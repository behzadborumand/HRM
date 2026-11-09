using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Pages.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Pages.Leave;

[Authorize]
public sealed class IndexModel(AccessService access, LeaveService service, LeaveDbContext db, EmployeeService employees) : PageModel
{
    [BindProperty] public string Name { get; set; } = "";
    [BindProperty] public decimal AnnualHours { get; set; }
    [BindProperty] public int EmployeeId { get; set; }
    [BindProperty] public int PolicyId { get; set; }
    [BindProperty] public string StartDateJalali { get; set; } = "";
    [BindProperty] public string EndDateJalali { get; set; } = "";
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public int RequestId { get; set; }
    [BindProperty] public string? Comments { get; set; }

    public IReadOnlyList<LeavePolicy> Policies { get; private set; } = [];
    public IReadOnlyList<LeaveRequest> Requests { get; private set; } = [];
    public EmployeePage? EmployeeChoices { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        await LoadAsync(actor, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostPolicyAsync(CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try { await service.CreatePolicyAsync(actor, Name, AnnualHours, null, null, ct); }
        catch (ArgumentException error) { return await ErrorAsync(actor, error.Message, ct); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRequestAsync(CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        if (!JalaliDate.TryParse(StartDateJalali, out var start) || !JalaliDate.TryParse(EndDateJalali, out var end))
            return await ErrorAsync(actor, "تاریخ شمسی معتبر وارد کنید.", ct);
        try { await service.CreateRequestAsync(actor, EmployeeId, PolicyId, start, end, Reason, ct); }
        catch (ArgumentException error) { return await ErrorAsync(actor, error.Message, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        return RedirectToPage();
    }

    public Task<IActionResult> OnPostApproveAsync(CancellationToken ct) => DecideAsync(true, ct);
    public Task<IActionResult> OnPostRejectAsync(CancellationToken ct) => DecideAsync(false, ct);

    public async Task<IActionResult> OnPostCancelAsync(CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try { await service.CancelAsync(actor, RequestId, ct); }
        catch (ArgumentException error) { return await ErrorAsync(actor, error.Message, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        return RedirectToPage();
    }

    private async Task<IActionResult> DecideAsync(bool approve, CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try { await service.DecideAsync(actor, RequestId, approve, Comments, ct); }
        catch (ArgumentException error) { return await ErrorAsync(actor, error.Message, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        return RedirectToPage();
    }

    public string ActorLabel(int? userId, int? employeeId)
    {
        if (employeeId is { } id)
            return EmployeeChoices?.Rows.FirstOrDefault(x => x.Id == id)?.FullName ?? $"کارمند #{id}";
        return userId is { } actorId ? $"کاربر #{actorId}" : "—";
    }

    private async Task<ActorScope?> AdminAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        return actor is { IsActive: true, IsAdmin: true } ? actor : null;
    }

    private async Task LoadAsync(ActorScope actor, CancellationToken ct)
    {
        Policies = await db.Policies.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.Active)
            .OrderBy(x => x.Name).ToListAsync(ct);
        Requests = await db.Requests.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId)
            .OrderByDescending(x => x.RequestedAtUtc).Take(100).ToListAsync(ct);
        EmployeeChoices = await employees.SearchAsync(actor, null, null, 1, 100, ct);
    }

    private async Task<IActionResult> ErrorAsync(ActorScope actor, string message, CancellationToken ct)
    {
        ModelState.AddModelError("", message);
        await LoadAsync(actor, ct);
        return Page();
    }
}
