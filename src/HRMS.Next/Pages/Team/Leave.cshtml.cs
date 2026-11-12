using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace HRMS.Next.Pages.Team;
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LeaveModel(AccessService access, TeamLeaveBoard board, LeaveService leave) : PageModel
{
    public TeamLeavePage? Requests { get; private set; }
    public async Task<IActionResult> OnGetAsync(int pageNumber = 1, CancellationToken ct = default)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (!AccessRules.CanUseTeam(actor)) return Forbid();
        Requests = await board.LoadAsync(actor!, pageNumber, ct);
        return Page();
    }
    public async Task<IActionResult> OnPostDecideAsync(int id, string? decision, string? version, int pageNumber = 1, CancellationToken ct = default)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (!AccessRules.CanUseTeam(actor)) return Forbid();
        try
        {
            if (!ModelState.IsValid || decision is not ("Approve" or "Reject") || string.IsNullOrEmpty(version))
                throw new ArgumentException("درخواست بررسی معتبر نیست.");
            await leave.DecideAsync(actor!, id, decision == "Approve", null, ct, Convert.FromBase64String(version));
            TempData["TeamLeaveMessage"] = decision == "Approve" ? "درخواست مرخصی تأیید شد." : "درخواست مرخصی رد شد.";
            return RedirectToPage(new { pageNumber });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception error) when (error is ArgumentException or FormatException or DbUpdateConcurrencyException or LeaveBusyException)
        {
            ModelState.AddModelError("", error switch
            {
                DbUpdateConcurrencyException => "درخواست تغییر کرده است؛ صفحه را تازه کنید.",
                FormatException => "نسخهٔ درخواست معتبر نیست.",
                ArgumentException => "درخواست قابل بررسی نیست؛ ممکن است قبلاً بررسی شده یا مانده کافی نباشد.",
                _ => error.Message
            });
        }
        Requests = await board.LoadAsync(actor!, pageNumber, ct);
        return Page();
    }
}