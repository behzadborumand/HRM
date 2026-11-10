namespace HRMS.Next.Modules.Organization;

public sealed record DepartmentOption(int Id, string Name);

public interface IOrganizationDirectory
{
    Task<bool> CompanyExistsAsync(int companyId, CancellationToken ct);
    Task<bool> DepartmentBelongsAsync(int departmentId, int companyId, CancellationToken ct);
    Task<IReadOnlyList<DepartmentOption>> DepartmentsAsync(int companyId, CancellationToken ct);
    Task<bool> ActiveJobTitleExistsAsync(int companyId, string name, CancellationToken ct);
}
