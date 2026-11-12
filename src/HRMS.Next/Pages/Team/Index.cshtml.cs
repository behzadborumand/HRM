using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace HRMS.Next.Pages.Team;
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IndexModel(AccessService access, PersonnelTeamService teams) : PageModel
{
    public TeamPage? Team { get; private set; }
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }
    public async Task<IActionResult> OnGetAsync(int pageNumber = 1, CancellationToken ct = default)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (!AccessRules.CanUseTeam(actor)) return Forbid();
        Team = await teams.ListAsync(actor!, Query, pageNumber, ct);
        return Page();
    }
}