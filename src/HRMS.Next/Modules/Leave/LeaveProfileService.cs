using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using Microsoft.EntityFrameworkCore;
namespace HRMS.Next.Modules.Leave;

public sealed record ProfileLeaveBalance(string PolicyName, decimal AccruedHours, decimal UsedHours, decimal PendingHours, decimal AvailableHours);
public sealed record ProfileLeaveRequest(int Id, string PolicyName, DateOnly StartDate, DateOnly EndDate, decimal Hours, string Status);
public sealed record EmployeeLeaveProfile(int Year, IReadOnlyList<ProfileLeaveBalance> Balances, IReadOnlyList<ProfileLeaveRequest> Requests, int Total, int Page, int PageSize);
public interface IEmployeeLeaveProfile
{
    Task<EmployeeLeaveProfile?> ReadAsync(ActorScope actor, int employeeId, int page, CancellationToken ct);
}
public sealed class LeaveProfileService(LeaveDbContext db, IEmployeeDirectory employees, TimeProvider clock) : IEmployeeLeaveProfile
{
    public async Task<EmployeeLeaveProfile?> ReadAsync(ActorScope actor, int employeeId, int page, CancellationToken ct)
    {
        if (!AccessRules.CanReadEmployeeActivity(actor, actor.CompanyId, employeeId)) throw new UnauthorizedAccessException();
        if (await employees.GetAsync(actor.CompanyId, employeeId, ct) is null) return null;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
        var year = today.Year;
        var requests = db.Requests.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId);
        var pending = await requests.Where(x => x.Status == "Pending" && x.StartDate.Year == year)
            .GroupBy(x => x.PolicyId).Select(g => new { PolicyId = g.Key, Hours = g.Sum(x => x.Hours) }).ToDictionaryAsync(x => x.PolicyId, x => x.Hours, ct);
        var stored = await (from balance in db.Balances.AsNoTracking()
            join policy in db.Policies.AsNoTracking() on new { Id = balance.PolicyId, balance.CompanyId } equals new { policy.Id, policy.CompanyId }
            where balance.CompanyId == actor.CompanyId && balance.EmployeeId == employeeId && balance.Year == year
            orderby policy.Name, policy.Id
            select new { policy.Id, policy.Name, balance.AccruedHours, balance.UsedHours }).ToListAsync(ct);
        var balances = stored.Select(x => new ProfileLeaveBalance(x.Name, x.AccruedHours, x.UsedHours,
            pending.GetValueOrDefault(x.Id), x.AccruedHours - x.UsedHours - pending.GetValueOrDefault(x.Id))).ToList();
        var history = from request in requests
            join policy in db.Policies.AsNoTracking() on new { Id = request.PolicyId, request.CompanyId } equals new { policy.Id, policy.CompanyId }
            select new { request.Id, policy.Name, request.StartDate, request.EndDate, request.Hours, request.Status };
        var total = await history.CountAsync(ct);
        const int pageSize = 20;
        page = Math.Clamp(page, 1, Math.Max(1, (total - 1) / pageSize + 1));
        var rows = await history.OrderByDescending(x => x.StartDate).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(year, balances, rows.Select(x => new ProfileLeaveRequest(x.Id, x.Name, x.StartDate, x.EndDate, x.Hours, x.Status)).ToList(), total, page, pageSize);
    }
}
