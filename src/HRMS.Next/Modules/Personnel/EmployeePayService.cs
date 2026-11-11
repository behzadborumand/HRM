using HRMS.Next.Modules.Access;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Personnel;

public sealed record PayHistoryRow(int JobId, DateOnly StartDate, DateOnly? EndDate,
    bool IsCurrent, string? PayType, decimal? Rate);

public sealed class EmployeePayService(PersonnelDbContext db)
{
    public async Task<IReadOnlyList<PayHistoryRow>> HistoryAsync(ActorScope actor, int employeeId, CancellationToken ct)
    {
        if (!AccessRules.CanReadPay(actor, actor.CompanyId, employeeId)) throw new UnauthorizedAccessException();
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.CompanyId == actor.CompanyId && x.Id == employeeId, ct))
            return [];
        return await db.EmployeeJobs.AsNoTracking()
            .Where(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId)
            .OrderByDescending(x => x.StartDate).ThenByDescending(x => x.Id)
            .Select(x => new PayHistoryRow(x.Id, x.StartDate, x.EndDate, x.IsCurrent, x.PayType, x.PayRate))
            .ToListAsync(ct);
    }
}
