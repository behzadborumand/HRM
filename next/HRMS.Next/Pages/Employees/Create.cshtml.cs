using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Modules.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages.Employees;

[Authorize]
public sealed class CreateModel(AccessService access, EmployeeService employees, IOrganizationDirectory organization) : PageModel
{
    [BindProperty] public EmployeeForm Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        ViewData["Departments"] = await organization.DepartmentsAsync(actor.CompanyId, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        ViewData["Departments"] = await organization.DepartmentsAsync(actor.CompanyId, ct);
        if (!ModelState.IsValid) return Page();
        try
        {
            var id = await employees.CreateAsync(actor, Form.ToInput(), ct);
            return RedirectToPage("Details", new { id });
        }
        catch (ArgumentException error)
        {
            ModelState.AddModelError("", error.Message);
            return Page();
        }
    }
}
