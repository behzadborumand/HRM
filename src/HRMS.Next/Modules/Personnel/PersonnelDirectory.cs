using Microsoft.EntityFrameworkCore;
using HRMS.Next.Modules.Access;

namespace HRMS.Next.Modules.Personnel;

public sealed record EmployeeSummary(int Id, int CompanyId, DateOnly HireDate, DateOnly? TerminationDate);

public interface IEmployeeDirectory
{
    Task<EmployeeSummary?> GetAsync(int companyId, int employeeId, CancellationToken ct);
    Task<bool> IsDirectManagerAsync(int companyId, int managerId, int employeeId, CancellationToken ct) => Task.FromResult(false);
}

public sealed class PersonnelDirectory(PersonnelDbContext db) : IEmployeeDirectory
{
    public Task<EmployeeSummary?> GetAsync(int companyId, int employeeId, CancellationToken ct) =>
        db.Employees.AsNoTracking().Where(x => x.CompanyId == companyId && x.Id == employeeId)
            .Select(x => new EmployeeSummary(x.Id, x.CompanyId, x.HireDate, x.TerminationDate))
            .SingleOrDefaultAsync(ct);

    public Task<bool> IsDirectManagerAsync(int companyId, int managerId, int employeeId, CancellationToken ct) =>
        PersonnelReadScope.DirectReports(db, companyId, managerId).AnyAsync(x => x.Id == employeeId, ct);
}

internal static class PersonnelReadScope
{
    internal static IQueryable<Employee> DirectReports(PersonnelDbContext db, int companyId, int managerId) =>
        db.Employees.AsNoTracking().Where(e => companyId > 0 && managerId > 0 && e.CompanyId == companyId &&
            e.Id != managerId && e.TerminationDate == null &&
            db.Employees.Any(m => m.Id == managerId && m.CompanyId == companyId && m.TerminationDate == null) &&
            db.EmployeeJobs.Any(j => j.CompanyId == companyId && j.EmployeeId == e.Id && j.IsCurrent && j.ManagerEmployeeId == managerId));

    internal static async Task RequireAsync(PersonnelDbContext db, ActorScope actor, int employeeId, CancellationToken ct)
    {
        if (AccessRules.CanReadEmployee(actor, actor.CompanyId, employeeId)) return;
        if (AccessRules.CanUseTeam(actor) && await DirectReports(db, actor.CompanyId, actor.EmployeeId!.Value).AnyAsync(x => x.Id == employeeId, ct)) return;
        throw new UnauthorizedAccessException();
    }
}