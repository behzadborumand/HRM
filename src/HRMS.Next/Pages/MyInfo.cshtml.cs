using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyInfoModel(AccessService access, EmployeeService employees) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor?.EmployeeId is not > 0) return Forbid();
        var employee = await employees.GetAsync(actor, actor.EmployeeId.Value, ct);
        return employee is null ? NotFound() : RedirectToPage("/Employees/Details", new { id = employee.Id });
    }
}
