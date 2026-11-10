using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Documents;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages.Employees;

[Authorize]
public sealed class DetailsModel(AccessService access, EmployeeService employees, IOrganizationDirectory organization, DocumentService documents) : PageModel
{
    public Employee? Employee { get; private set; }
    public bool CanWrite { get; private set; }
    public string? DepartmentName { get; private set; }
    public DocumentPage? Documents { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanRead(actor, actor.CompanyId)) return Forbid();
        Employee = await employees.GetAsync(actor, id, ct);
        CanWrite = AccessRules.CanWrite(actor, actor.CompanyId);
        if (Employee is not null)
        {
            DepartmentName = (await organization.DepartmentsAsync(actor.CompanyId, ct))
                .FirstOrDefault(x => x.Id == Employee.DepartmentId)?.Name;
            Documents = await documents.ListAsync(actor, id, 1, 20, ct);
        }
        return Employee is null ? NotFound() : Page();
    }
}
