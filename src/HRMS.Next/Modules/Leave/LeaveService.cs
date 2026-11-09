using System.Data;
using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Leave;

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
    public async Task<int> CreateRequestAsync(ActorScope actor, int employeeId, int policyId, DateOnly start, DateOnly end, string? reason, CancellationToken ct)
    {
        RequireAdmin(actor);
        var employee = await employees.GetAsync(actor.CompanyId, employeeId, ct)
            ?? throw new UnauthorizedAccessException("Employee is outside the active company.");
        LeaveRules.ValidateRange(start, end, employee.HireDate, employee.TerminationDate);
        if (start.Year != end.Year) throw new ArgumentException("A leave request must stay within one balance year.");
        if (reason?.Length > 1000) throw new ArgumentException("Reason is too long.");
        var hours = LeaveRules.WorkingHours(start, end);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var policy = await db.Policies.SingleOrDefaultAsync(x => x.Id == policyId && x.CompanyId == actor.CompanyId && x.Active, ct)
            ?? throw new ArgumentException("Active leave policy was not found.");
        if (await db.Requests.AnyAsync(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId &&
            (x.Status == "Pending" || x.Status == "Approved") && x.StartDate <= end && x.EndDate >= start, ct))
            throw new ArgumentException("Leave dates overlap another request.");
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
                throw new ArgumentException("Insufficient leave balance.");
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

    public async Task DecideAsync(ActorScope actor, int requestId, bool approve, string? comments, CancellationToken ct)
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

    private static void RequireAdmin(ActorScope actor)
    {
        if (!actor.IsActive || !actor.IsAdmin || actor.CompanyId <= 0) throw new UnauthorizedAccessException();
    }
}