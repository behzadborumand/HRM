using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Modules.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Pages.Employees;

[Authorize]
public sealed class EditModel(AccessService access, EmployeeService employees, IOrganizationDirectory organization) : PageModel
{
    [BindProperty] public EmployeeForm Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        var employee = await employees.GetAsync(actor, id, ct);
        if (employee is null) return NotFound();
        ViewData["Departments"] = await organization.DepartmentsAsync(actor.CompanyId, ct);
        Form = EmployeeForm.From(employee);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        ViewData["Departments"] = await organization.DepartmentsAsync(actor.CompanyId, ct);
        if (!ModelState.IsValid) return Page();
        try
        {
            var version = Convert.FromBase64String(Form.Version ?? "");
            await employees.UpdateAsync(actor, id, Form.ToInput(), version, ct);
            return RedirectToPage("Details", new { id });
        }
        catch (FormatException) { ModelState.AddModelError("", "نسخه پرونده نامعتبر است."); }
        catch (DbUpdateConcurrencyException error) { ModelState.AddModelError("", error.Message); }
        catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
        return Page();
    }
}
