using HRMS.Next.Modules.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace HRMS.Next.Pages.My;
[Authorize]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PersonalModel(AccessService access):PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var actor=await access.GetActiveAsync(User,ct);
        return actor?.EmployeeId is >0?RedirectToPage("/Employees/Personal",new{id=actor.EmployeeId.Value}):Forbid();
    }
}
