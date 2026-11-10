using HRMS.Next.Modules.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages.Settings;

[Authorize]
public sealed class IndexModel(AccessService access) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        return actor is { IsActive: true, IsAdmin: true } ? Page() : Forbid();
    }
}
