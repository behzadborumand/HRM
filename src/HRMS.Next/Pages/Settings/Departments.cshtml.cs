using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Pages.Settings;

[Authorize]
public sealed class DepartmentsModel(AccessService access, DepartmentService departments, EmployeeService employees) : PageModel
{
    [BindProperty] public int EditId { get; set; }
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public int? ParentId { get; set; }
    [BindProperty] public string Version { get; set; } = "";
    public IReadOnlyList<Department> Rows { get; private set; } = [];
    public IReadOnlyDictionary<int, int> Headcounts { get; private set; } = new Dictionary<int, int>();
    public string ParentName(int? id) => id is { } value ? Rows.FirstOrDefault(x => x.Id == value)?.Name ?? "-" : "-";

    public async Task<IActionResult> OnGetAsync(int? edit, CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        await LoadAsync(actor, ct);
        if (edit is { } id)
        {
            var row = Rows.FirstOrDefault(x => x.Id == id);
            if (row is null) return NotFound();
            EditId = row.Id;
            Name = row.Name;
            ParentId = row.ParentId;
            Version = Convert.ToBase64String(row.RowVersion);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try
        {
            if (EditId == 0) await departments.CreateAsync(actor, Name, ParentId, ct);
            else await departments.UpdateAsync(actor, EditId, Name, ParentId, Convert.FromBase64String(Version), ct);
            return RedirectToPage();
        }
        catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
        catch (FormatException) { ModelState.AddModelError("", "نسخهٔ ثبت معتبر نیست."); }
        catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "واحد توسط کاربر دیگری تغییر کرده است. صفحه را بازخوانی کنید."); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        await LoadAsync(actor, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, bool active, string version, CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try
        {
            await departments.SetActiveAsync(actor, id, active, Convert.FromBase64String(version), ct);
            return RedirectToPage();
        }
        catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
        catch (FormatException) { ModelState.AddModelError("", "نسخهٔ ثبت معتبر نیست."); }
        catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "واحد توسط کاربر دیگری تغییر کرده است. صفحه را بازخوانی کنید."); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        await LoadAsync(actor, ct);
        return Page();
    }

    private async Task LoadAsync(ActorScope actor, CancellationToken ct)
    {
        Rows = await departments.ListAsync(actor, ct);
        Headcounts = await employees.DepartmentHeadcountsAsync(actor, ct);
    }

    private async Task<ActorScope?> AdminAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        return actor is { IsActive: true, IsAdmin: true } ? actor : null;
    }
}
