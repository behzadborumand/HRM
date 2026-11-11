using HRMS.Next.Modules.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Pages.Settings;

[Authorize]
public sealed class UsersModel(AccessService access, AccountAdministrationService accounts) : PageModel
{
    [BindProperty] public string NewUsername { get; set; } = "";
    [BindProperty] public string NewPassword { get; set; } = "";
    [BindProperty] public int? NewEmployeeId { get; set; }
    [BindProperty] public bool NewCanRead { get; set; }
    [BindProperty] public bool NewCanWrite { get; set; }
    public AccountPage? Accounts { get; private set; }

    public async Task<IActionResult> OnGetAsync(int page = 1, CancellationToken ct = default)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        Accounts = await accounts.ListAsync(actor, page, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(int page = 1, CancellationToken ct = default)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try
        {
            await accounts.CreateAsync(actor,
                new AccountCreateInput(NewUsername, NewPassword, NewEmployeeId, NewCanRead, NewCanWrite), ct);
            return RedirectToPage("/Settings/Users", new { page });
        }
        catch (Exception error) when (error is ArgumentException or DbUpdateException)
        {
            ModelState.AddModelError("", error is DbUpdateException ? "حساب یا پیوند کارمند تکراری است." : error.Message);
            NewPassword = "";
            Accounts = await accounts.ListAsync(actor, page, ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, int? employeeId, bool canRead, bool canWrite,
        bool isActive, string version, int page = 1, CancellationToken ct = default)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try
        {
            await accounts.UpdateAsync(actor, id, new AccountUpdateInput(employeeId, canRead, canWrite, isActive),
                Convert.FromBase64String(version), ct);
            return RedirectToPage("/Settings/Users", new { page });
        }
        catch (Exception error) when (error is ArgumentException or FormatException or KeyNotFoundException or DbUpdateException)
        {
            ModelState.AddModelError("", error switch
            {
                DbUpdateConcurrencyException => "حساب توسط کاربر دیگری تغییر کرده است. صفحه را بازخوانی کنید.",
                DbUpdateException => "پیوند کارمند تکراری است.",
                FormatException => "نسخهٔ حساب معتبر نیست.",
                _ => error.Message
            });
            Accounts = await accounts.ListAsync(actor, page, ct);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostResetAsync(int id, string newPassword, int page = 1,
        CancellationToken ct = default)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try
        {
            await accounts.ResetPasswordAsync(actor, id, newPassword, ct);
            return RedirectToPage("/Settings/Users", new { page });
        }
        catch (Exception error) when (error is ArgumentException or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", error is DbUpdateConcurrencyException ? "حساب توسط کاربر دیگری تغییر کرده است. صفحه را بازخوانی کنید." : error.Message);
            Accounts = await accounts.ListAsync(actor, page, ct);
            return Page();
        }
    }

    private async Task<ActorScope?> AdminAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        return actor is { IsActive: true, IsAdmin: true } ? actor : null;
    }
}
