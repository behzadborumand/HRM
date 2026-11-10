using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Pages.Settings;

[Authorize]
public sealed class JobTitlesModel(AccessService access, JobTitleService titles) : PageModel
{
    [BindProperty] public int EditId { get; set; }
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string Version { get; set; } = "";
    public IReadOnlyList<JobTitle> Rows { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? edit, CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        Rows = await titles.ListAsync(actor, ct);
        if (edit is { } id)
        {
            var row = Rows.FirstOrDefault(x => x.Id == id);
            if (row is null) return NotFound();
            EditId = row.Id;
            Name = row.Name;
            Description = row.Description;
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
            if (EditId == 0) await titles.CreateAsync(actor, Name, Description, ct);
            else await titles.UpdateAsync(actor, EditId, Name, Description, Convert.FromBase64String(Version), ct);
            return RedirectToPage();
        }
        catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
        catch (FormatException) { ModelState.AddModelError("", "نسخهٔ ثبت معتبر نیست."); }
        catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "عنوان شغلی توسط کاربر دیگری تغییر کرده است. صفحه را بازخوانی کنید."); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        Rows = await titles.ListAsync(actor, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, bool active, string version, CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try
        {
            await titles.SetActiveAsync(actor, id, active, Convert.FromBase64String(version), ct);
            return RedirectToPage();
        }
        catch (FormatException) { ModelState.AddModelError("", "نسخهٔ ثبت معتبر نیست."); }
        catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "عنوان شغلی توسط کاربر دیگری تغییر کرده است. صفحه را بازخوانی کنید."); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        Rows = await titles.ListAsync(actor, ct);
        return Page();
    }

    private async Task<ActorScope?> AdminAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        return actor is { IsActive: true, IsAdmin: true } ? actor : null;
    }
}
