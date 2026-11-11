using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Pages.Settings;

[Authorize]
public sealed class OrganizationLinksModel(AccessService access, OrganizationLinkService links) : PageModel
{
    [BindProperty] public int EditId { get; set; }
    [BindProperty] public string Title { get; set; } = "";
    [BindProperty] public string LinkUrl { get; set; } = "";
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public int SortOrder { get; set; }
    [BindProperty] public bool Active { get; set; }
    [BindProperty] public string Version { get; set; } = "";
    public IReadOnlyList<OrganizationLink> Rows { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? edit, CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        Rows = await links.ListAsync(actor, ct);
        Active = true;
        if (edit is { } id)
        {
            var row = Rows.FirstOrDefault(x => x.Id == id);
            if (row is null) return NotFound();
            EditId = row.Id; Title = row.Title; LinkUrl = row.Url; Description = row.Description;
            SortOrder = row.SortOrder; Active = row.Active; Version = Convert.ToBase64String(row.RowVersion);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try
        {
            if (EditId == 0) await links.CreateAsync(actor, Title, LinkUrl, Description, SortOrder, Active, ct);
            else await links.UpdateAsync(actor, EditId, Title, LinkUrl, Description, SortOrder, Active, Convert.FromBase64String(Version), ct);
            return RedirectToPage();
        }
        catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
        catch (FormatException) { ModelState.AddModelError("", "نسخهٔ ثبت معتبر نیست."); }
        catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "پیوند توسط کاربر دیگری تغییر کرده است. صفحه را بازخوانی کنید."); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        Rows = await links.ListAsync(actor, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, string version, CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try
        {
            await links.DeleteAsync(actor, id, Convert.FromBase64String(version), ct);
            return RedirectToPage();
        }
        catch (FormatException) { ModelState.AddModelError("", "نسخهٔ ثبت معتبر نیست."); }
        catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "پیوند توسط کاربر دیگری تغییر کرده است. صفحه را بازخوانی کنید."); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        Rows = await links.ListAsync(actor, ct);
        return Page();
    }

    private async Task<ActorScope?> AdminAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        return actor is { IsActive: true, IsAdmin: true } ? actor : null;
    }
}
