using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Modules.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages;

[Authorize]
public sealed class IndexModel(AccessService access, OrganizationLinkService links, EmployeeService employees, HomeHighlightsService highlights, ReportingService reports, IEmployeeLeaveProfile leave) : PageModel
{
    public string Username { get; private set; } = "";
    public int? LinkedEmployeeId { get; private set; }
    public EmployeeLeaveProfile? OwnLeave { get; private set; }
    public bool CanBrowsePeople { get; private set; }
    public bool CanWriteEmployees { get; private set; }
    public bool CanUseTeam { get; private set; }
    public bool IsAdmin { get; private set; }
    public int EmployeeCount { get; private set; }
    public HomeHighlights? Highlights { get; private set; }
    public ReportingDashboard? Stats { get; private set; }
    public IReadOnlyList<OrganizationLink> OrganizationLinks { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null) return Forbid();
        Username = actor.Username;
        CanBrowsePeople = AccessRules.CanRead(actor, actor.CompanyId);
        CanWriteEmployees = AccessRules.CanWrite(actor, actor.CompanyId);
        IsAdmin = actor.IsAdmin;
        CanUseTeam = AccessRules.CanUseTeam(actor);
        OrganizationLinks = await links.ActiveAsync(actor, ct);
        if (CanBrowsePeople)
            {
            EmployeeCount = (await employees.SearchAsync(actor, null, null, 1, 1, ct)).Total;
            Highlights = await highlights.LoadAsync(actor, ct);
        }
        if (actor.EmployeeId is > 0 && await employees.GetAsync(actor, actor.EmployeeId.Value, ct) is { } own)
        {
            LinkedEmployeeId = own.Id;
            OwnLeave = await leave.ReadAsync(actor, own.Id, 1, ct);
        }
        if (IsAdmin) Stats = await reports.DashboardAsync(actor, ct);
        return Page();
    }
}
