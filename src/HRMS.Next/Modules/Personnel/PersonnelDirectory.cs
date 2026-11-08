using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Personnel;

public sealed record EmployeeSummary(int Id, int CompanyId, DateOnly HireDate, DateOnly? TerminationDate);

public interface IEmployeeDirectory
{
    Task<EmployeeSummary?> GetAsync(int companyId, int employeeId, CancellationToken ct);
}

public sealed class PersonnelDirectory(PersonnelDbContext db) : IEmployeeDirectory
{
    public Task<EmployeeSummary?> GetAsync(int companyId, int employeeId, CancellationToken ct) =>
        db.Employees.AsNoTracking().Where(x => x.CompanyId == companyId && x.Id == employeeId)
            .Select(x => new EmployeeSummary(x.Id, x.CompanyId, x.HireDate, x.TerminationDate))
            .SingleOrDefaultAsync(ct);
}