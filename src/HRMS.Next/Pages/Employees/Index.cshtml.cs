using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages.Employees;

[Authorize]
public sealed class IndexModel(AccessService access, EmployeeService employees, IOrganizationDirectory organization) : PageModel
{
    public string? Query { get; private set; }
    public int? DepartmentId { get; private set; }
    public string? Status { get; private set; }
    public EmployeePage? Result { get; private set; }
    public IReadOnlyList<DepartmentOption> Departments { get; private set; } = [];
    public bool CanWrite { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? query, int? departmentId, string? status, int pageNumber = 1, CancellationToken ct = default)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanRead(actor, actor.CompanyId)) return Forbid();
        Query = query;
        DepartmentId = departmentId;
        Status = status is "Active" or "Terminated" or "OnLeave" ? status : null;
        CanWrite = AccessRules.CanWrite(actor, actor.CompanyId);
        Departments = await organization.DepartmentsAsync(actor.CompanyId, ct);
        Result = await employees.SearchAsync(actor, query, departmentId, pageNumber, 20, ct, Status);
        return Page();
    }
}
