using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Modules.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Pages.Employees;

[Authorize]
public sealed class EditModel(AccessService access, EmployeeService employees, EmployeeJobService jobs,
    IOrganizationDirectory organization) : PageModel
{
    [BindProperty, ValidateNever] public EmployeeForm Form { get; set; } = new();
    [BindProperty, ValidateNever] public JobChangeForm JobForm { get; set; } = new();
    public IReadOnlyList<Employee> Managers { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        var employee = await employees.GetAsync(actor, id, ct);
        if (employee is null) return NotFound();
        await LoadOptionsAsync(actor, id, ct);
        Form = EmployeeForm.From(employee);
        var history = await jobs.HistoryAsync(actor, id, ct);
        JobForm = JobChangeForm.From(employee, history.FirstOrDefault(x => x.IsCurrent));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        foreach (var key in ModelState.Keys.Where(x => !x.Contains('.')).ToList())
            ModelState.Remove(key);
        TryValidateModel(Form, nameof(Form));
        await LoadOptionsAsync(actor, id, ct);
        var employee = await employees.GetAsync(actor, id, ct);
        if (employee is null) return NotFound();
        var history = await jobs.HistoryAsync(actor, id, ct);
        JobForm = JobChangeForm.From(employee, history.FirstOrDefault(x => x.IsCurrent));
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

    public async Task<IActionResult> OnPostJobAsync(int id, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        foreach (var key in ModelState.Keys.Where(x => !x.Contains('.')).ToList())
            ModelState.Remove(key);
        TryValidateModel(JobForm, nameof(JobForm));
        await LoadOptionsAsync(actor, id, ct);
        var employee = await employees.GetAsync(actor, id, ct);
        if (employee is null) return NotFound();
        Form = EmployeeForm.From(employee);
        if (!ModelState.IsValid) return Page();
        try
        {
            var version = Convert.FromBase64String(JobForm.Version);
            await jobs.ChangeAsync(actor, id, JobForm.ToInput(), version, ct);
            return RedirectToPage("Details", new { id });
        }
        catch (FormatException) { ModelState.AddModelError("", "نسخه پرونده نامعتبر است."); }
        catch (DbUpdateConcurrencyException error) { ModelState.AddModelError("", error.Message); }
        catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
        return Page();
    }

    private async Task LoadOptionsAsync(ActorScope actor, int id, CancellationToken ct)
    {
        ViewData["Departments"] = await organization.DepartmentsAsync(actor.CompanyId, ct);
        ViewData["LockDepartment"] = true;
        Managers = await jobs.ManagerOptionsAsync(actor, id, ct);
    }
}
