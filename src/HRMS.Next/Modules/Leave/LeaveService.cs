using System.Data;
using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Leave;

public sealed record LeavePolicyChoice(int Id, string Name);
public sealed record OwnLeavePending(int Id, string PolicyName, DateOnly StartDate, DateOnly EndDate, decimal Hours, byte[] RowVersion);
public sealed record OwnLeavePendingPage(IReadOnlyList<OwnLeavePending> Rows, int Total, int Page, int PageSize);

public sealed class LeaveService(LeaveDbContext db, IEmployeeDirectory employees, TimeProvider clock)
{
    public async Task<int> CreatePolicyAsync(ActorScope actor, string name, decimal annualHours, string? category, string? description, CancellationToken ct)
    {
        RequireAdmin(actor);
        name = (name ?? "").Trim();
        if (name.Length is 0 or > 200) throw new ArgumentException("Policy name is required and must fit 200 characters.");
        if (annualHours < 0 || annualHours > 99999999m) throw new ArgumentException("Annual hours are invalid.");
        if (category?.Length > 50 || description?.Length > 1000) throw new ArgumentException("Policy text is too long.");
        if (await db.Policies.AnyAsync(x => x.CompanyId == actor.CompanyId && x.Name == name, ct))
            throw new ArgumentException("Policy name already exists in this company.");
        var policy = new LeavePolicy
        {
            CompanyId = actor.CompanyId, Name = name, AnnualHours = annualHours,
            Category = category?.Trim(), Description = description?.Trim(), Active = true
        };
        db.Policies.Add(policy);
        await db.SaveChangesAsync(ct);
        return policy.Id;
    }
    public Task<int> CreateRequestAsync(ActorScope actor, int employeeId, int policyId, DateOnly start, DateOnly end, string? reason, CancellationToken ct) =>
        LeaveTransaction.RunAsync(db, () => CreateRequestCoreAsync(actor, employeeId, policyId, start, end, reason, ct), ct);

    private async Task<int> CreateRequestCoreAsync(ActorScope actor, int employeeId, int policyId, DateOnly start, DateOnly end, string? reason, CancellationToken ct)
    {
        if (!AccessRules.CanReadEmployeeActivity(actor, actor.CompanyId, employeeId)) throw new UnauthorizedAccessException();
        var employee = await employees.GetAsync(actor.CompanyId, employeeId, ct)
            ?? throw new UnauthorizedAccessException("کارمند در شرکت فعال شما یافت نشد.");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
        if (!actor.IsAdmin && employee.TerminationDate is { } departed && departed <= today)
            throw new ArgumentException("برای همکاری خاتمه‌یافته نمی‌توان درخواست مرخصی جدید ثبت کرد.");
        LeaveRules.ValidateRange(start, end, employee.HireDate, employee.TerminationDate);
        if (start.Year != end.Year) throw new ArgumentException("درخواست مرخصی باید در یک سال حساب مانده باشد.");
        if (reason?.Length > 1000) throw new ArgumentException("علت مرخصی باید حداکثر ۱۰۰۰ نویسه باشد.");
        var hours = LeaveRules.WorkingHours(start, end);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var policy = await db.Policies.SingleOrDefaultAsync(x => x.Id == policyId && x.CompanyId == actor.CompanyId && x.Active, ct)
            ?? throw new ArgumentException("نوع مرخصی فعال یافت نشد.");
        if (await db.Requests.AnyAsync(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId &&
            (x.Status == "Pending" || x.Status == "Approved") && x.StartDate <= end && x.EndDate >= start, ct))
            throw new ArgumentException("تاریخ مرخصی با درخواست در انتظار یا تأییدشدهٔ دیگری هم‌پوشانی دارد.");
        if (policy.AnnualHours > 0)
        {
            var balance = await db.Balances.SingleOrDefaultAsync(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId &&
                x.PolicyId == policyId && x.Year == start.Year, ct);
            if (balance is null)
            {
                balance = new LeaveBalance
                {
                    CompanyId = actor.CompanyId, EmployeeId = employeeId, PolicyId = policyId, Year = start.Year,
                    AccruedHours = LeaveRules.ProrateAccrual(employee.HireDate, start.Year, policy.AnnualHours),
                    UpdatedAtUtc = clock.GetUtcNow()
                };
                db.Balances.Add(balance);
            }
            var pending = await db.Requests.Where(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId &&
                x.PolicyId == policyId && x.Status == "Pending" && x.StartDate.Year == start.Year)
                .SumAsync(x => x.Hours, ct);
            if (balance.AccruedHours - balance.UsedHours - pending < hours)
                throw new ArgumentException("ماندهٔ مرخصی کافی نیست.");
        }
        var request = new LeaveRequest
        {
            CompanyId = actor.CompanyId, EmployeeId = employeeId, PolicyId = policyId, StartDate = start, EndDate = end,
            Hours = hours, Reason = reason?.Trim(), Status = "Pending", RequestedByUserId = actor.Id,
            RequestedAtUtc = clock.GetUtcNow()
        };
        db.Requests.Add(request);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return request.Id;
    }

    public async Task DecideAsync(ActorScope actor, int requestId, bool approve, string? comments, CancellationToken ct) =>
        await LeaveTransaction.RunAsync(db, async () => { await DecideCoreAsync(actor, requestId, approve, comments, ct); return true; }, ct);

    private async Task DecideCoreAsync(ActorScope actor, int requestId, bool approve, string? comments, CancellationToken ct)
    {
        RequireAdmin(actor);
        if (comments?.Length > 1000) throw new ArgumentException("Comments are too long.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await db.Requests.SingleOrDefaultAsync(x => x.Id == requestId && x.CompanyId == actor.CompanyId, ct)
            ?? throw new UnauthorizedAccessException("Request is outside the active company.");
        if (request.Status != "Pending") throw new ArgumentException("Request is already decided.");
        if (approve)
        {
            var policy = await db.Policies.SingleAsync(x => x.Id == request.PolicyId && x.CompanyId == actor.CompanyId, ct);
            if (policy.AnnualHours > 0)
            {
                var balance = await db.Balances.SingleAsync(x => x.CompanyId == actor.CompanyId && x.EmployeeId == request.EmployeeId &&
                    x.PolicyId == request.PolicyId && x.Year == request.StartDate.Year, ct);
                var otherPending = await db.Requests.Where(x => x.CompanyId == actor.CompanyId && x.EmployeeId == request.EmployeeId &&
                    x.PolicyId == request.PolicyId && x.Id != requestId && x.Status == "Pending" && x.StartDate.Year == request.StartDate.Year)
                    .SumAsync(x => x.Hours, ct);
                if (balance.AccruedHours - balance.UsedHours - otherPending < request.Hours)
                    throw new ArgumentException("Insufficient leave balance for approval.");
                balance.UsedHours += request.Hours;
                balance.UpdatedAtUtc = clock.GetUtcNow();
            }
        }
        request.Status = approve ? "Approved" : "Rejected";
        request.DecidedByUserId = actor.Id;
        request.DecidedAtUtc = clock.GetUtcNow();
        request.Comments = comments?.Trim();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task CancelAsync(ActorScope actor, int requestId, CancellationToken ct)
    {
        RequireAdmin(actor);
        var request = await db.Requests.SingleOrDefaultAsync(x => x.Id == requestId && x.CompanyId == actor.CompanyId, ct)
            ?? throw new UnauthorizedAccessException("Request is outside the active company.");
        if (request.Status != "Pending") throw new ArgumentException("Only pending requests can be cancelled.");
        request.Status = "Cancelled";
        request.DecidedByUserId = actor.Id;
        request.DecidedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LeavePolicyChoice>> RequestPoliciesAsync(ActorScope actor, CancellationToken ct)
    {
        await RequireOwnerAsync(actor, ct);
        return await db.Policies.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.Active)
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new LeavePolicyChoice(x.Id, x.Name)).ToListAsync(ct);
    }

    public async Task<OwnLeavePendingPage> PendingOwnAsync(ActorScope actor, int page, CancellationToken ct)
    {
        var employeeId = await RequireOwnerAsync(actor, ct);
        var query = from request in db.Requests.AsNoTracking()
            join policy in db.Policies.AsNoTracking() on new { Id=request.PolicyId, request.CompanyId } equals new { policy.Id, policy.CompanyId }
            where request.CompanyId == actor.CompanyId && request.EmployeeId == employeeId && request.Status == "Pending"
            select new { request.Id, policy.Name, request.StartDate, request.EndDate, request.Hours, request.RowVersion };
        var total = await query.CountAsync(ct);
        const int size = 20;
        page = Math.Clamp(page, 1, Math.Max(1, (total - 1) / size + 1));
        var rows = await query.OrderByDescending(x => x.StartDate).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(x => new OwnLeavePending(x.Id, x.Name, x.StartDate, x.EndDate, x.Hours, x.RowVersion)).ToList(), total, page, size);
    }

    public async Task CancelOwnAsync(ActorScope actor, int requestId, byte[] expectedVersion, CancellationToken ct)
    {
        var employeeId = await RequireOwnerAsync(actor, ct);
        var request = await db.Requests.SingleOrDefaultAsync(x => x.Id == requestId && x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId, ct)
            ?? throw new UnauthorizedAccessException();
        if (!request.RowVersion.SequenceEqual(expectedVersion))
            throw new DbUpdateConcurrencyException("درخواست تغییر کرده است؛ صفحه را بازخوانی کنید.");
        if (request.Status != "Pending") throw new ArgumentException("فقط درخواست در انتظار تأیید قابل لغو است.");
        request.Status = "Cancelled";
        request.DecidedByUserId = actor.Id;
        request.DecidedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    private async Task<int> RequireOwnerAsync(ActorScope actor, CancellationToken ct)
    {
        if (!actor.IsActive || actor.CompanyId <= 0 || actor.EmployeeId is not > 0 ||
            await employees.GetAsync(actor.CompanyId, actor.EmployeeId.Value, ct) is null)
            throw new UnauthorizedAccessException();
        return actor.EmployeeId.Value;
    }

    private static void RequireAdmin(ActorScope actor)
    {
        if (!actor.IsActive || !actor.IsAdmin || actor.CompanyId <= 0) throw new UnauthorizedAccessException();
    }
}