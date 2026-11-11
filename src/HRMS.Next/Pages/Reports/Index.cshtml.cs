using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages.Reports;

[Authorize]
public sealed class IndexModel(AccessService access, ReportingService reports) : PageModel
{
    public ReportingDashboard? Stats { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is not { IsActive: true, IsAdmin: true }) return Forbid();
        Stats = await reports.DashboardAsync(actor, ct);
        return Page();
    }
}
