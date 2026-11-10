using HRMS.Next.Modules.Access;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Personnel;

public sealed record JobHistoryRow(int Id, DateOnly StartDate, DateOnly? EndDate, bool IsCurrent,
    int? DepartmentId, string? JobTitle, int? ManagerEmployeeId, string? ManagerName, string? EmploymentType);

public sealed class EmployeeJobService(PersonnelDbContext db)
{
    public async Task<IReadOnlyList<JobHistoryRow>> HistoryAsync(ActorScope actor, int employeeId, CancellationToken ct)
    {
        if (!AccessRules.CanRead(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.Id == employeeId && x.CompanyId == actor.CompanyId, ct))
            return [];
        return await db.EmployeeJobs.AsNoTracking()
            .Where(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId)
            .OrderByDescending(x => x.StartDate).ThenByDescending(x => x.Id)
            .Select(x => new JobHistoryRow(x.Id, x.StartDate, x.EndDate, x.IsCurrent, x.DepartmentId,
                x.JobTitle, x.ManagerEmployeeId,
                db.Employees.Where(m => m.Id == x.ManagerEmployeeId && m.CompanyId == actor.CompanyId)
                    .Select(m => m.FirstName + " " + m.LastName).FirstOrDefault(), x.EmploymentType))
            .ToListAsync(ct);
    }
}