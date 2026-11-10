using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Organization;

public sealed class OrganizationDirectory(OrganizationDbContext db) : IOrganizationDirectory
{
    public Task<bool> CompanyExistsAsync(int companyId, CancellationToken ct) =>
        db.Companies.AnyAsync(x => x.Id == companyId, ct);

    public Task<bool> DepartmentBelongsAsync(int departmentId, int companyId, CancellationToken ct) =>
        db.Departments.AnyAsync(x => x.Id == departmentId && x.CompanyId == companyId, ct);

    public Task<bool> ActiveJobTitleExistsAsync(int companyId, string name, CancellationToken ct) =>
        db.JobTitles.AsNoTracking().AnyAsync(x => x.CompanyId == companyId && x.IsActive && x.Name == name, ct);

    public async Task<IReadOnlyList<DepartmentOption>> DepartmentsAsync(int companyId, CancellationToken ct) =>
        await db.Departments.AsNoTracking().Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.Name).Select(x => new DepartmentOption(x.Id, x.Name)).ToListAsync(ct);
}
