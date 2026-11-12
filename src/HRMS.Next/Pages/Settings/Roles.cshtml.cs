using HRMS.Next.Modules.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Pages.Settings;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RolesModel(AccessService access, RoleAdministrationService roles) : PageModel
{
    [BindProperty] public int EditId { get; set; }
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public bool CanRead { get; set; }
    [BindProperty] public bool CanWrite { get; set; }
    [BindProperty] public bool CanManageTeam { get; set; }
    [BindProperty] public bool Active { get; set; } = true;
    [BindProperty] public string? Version { get; set; }
    public RolePage? Roles { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? edit, int pageNumber = 1, CancellationToken ct = default)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        Roles = await roles.ListAsync(actor, pageNumber, ct);
        if (edit is { } id)
        {
            var row = (await roles.ChoicesAsync(actor, ct)).SingleOrDefault(r => r.Id == id);
            if (row is null) return NotFound();
            EditId = row.Id; Name = row.Name; Description = row.Description;
            CanRead = row.CanReadEmployees; CanWrite = row.CanWriteEmployees;
            CanManageTeam = row.CanManageTeam; Active = row.Active;
            Version = Convert.ToBase64String(row.RowVersion);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(int pageNumber = 1, CancellationToken ct = default)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try
        {
            if (!ModelState.IsValid) throw new ArgumentException("اطلاعات فرم معتبر نیست.");
            await roles.SaveAsync(actor, EditId, new(Name, Description, CanRead, CanWrite, CanManageTeam, Active),
                EditId == 0 ? null : Convert.FromBase64String(Version ?? ""), ct);
            return RedirectToPage(new { pageNumber });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception error) when (error is ArgumentException or FormatException or DbUpdateException)
        {
            ModelState.AddModelError("", error switch
            {
                DbUpdateConcurrencyException => "نقش تغییر کرده است؛ صفحه را بازخوانی کنید.",
                DbUpdateException => "نام نقش تکراری است یا ذخیره انجام نشد.",
                FormatException => "نسخهٔ نقش معتبر نیست.",
                _ => error.Message
            });
        }
        Roles = await roles.ListAsync(actor, pageNumber, ct);
        return Page();
    }

    private async Task<ActorScope?> AdminAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        return actor is { IsActive: true, IsAdmin: true } ? actor : null;
    }
}