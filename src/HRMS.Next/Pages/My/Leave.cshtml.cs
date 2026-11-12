using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Pages.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Pages.My;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LeaveModel(AccessService access, EmployeeService employees, LeaveService leave, TimeProvider clock) : PageModel
{
    public int PolicyId { get; set; }
    public string StartDateJalali { get; set; } = "";
    public string EndDateJalali { get; set; } = "";
    public string? Reason { get; set; }
    public Employee? Employee { get; private set; }
    public IReadOnlyList<LeavePolicyChoice> Policies { get; private set; } = [];
    public OwnLeavePendingPage Pending { get; private set; } = new([], 0, 1, 20);
    public bool CanSubmit { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct, int pendingPage = 1)
    {
        var actor = await OwnerAsync(ct);
        if (actor is null) return Forbid();
        await LoadAsync(actor, pendingPage, ct);
        return Employee is null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostRequestAsync(int policyId, string? startDateJalali, string? endDateJalali, string? reason, CancellationToken ct)
    {
        var actor = await OwnerAsync(ct);
        if (actor is null) return Forbid();
        PolicyId = policyId;
        StartDateJalali = startDateJalali ?? "";
        EndDateJalali = endDateJalali ?? "";
        Reason = reason;
        if (!ModelState.IsValid) { ModelState.Clear(); return await ErrorAsync(actor, "اطلاعات فرم معتبر نیست؛ نوع مرخصی و تاریخ‌ها را بررسی کنید.", ct); }
        if (!JalaliDate.TryParse(StartDateJalali, out var start) || !JalaliDate.TryParse(EndDateJalali, out var end))
            return await ErrorAsync(actor, "تاریخ شروع و پایان شمسی معتبر وارد کنید.", ct);
        try { await leave.CreateRequestAsync(actor, actor.EmployeeId!.Value, PolicyId, start, end, Reason, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (LeaveBusyException error) { return await ErrorAsync(actor, error.Message, ct); }
        catch (ArgumentException error) { return await ErrorAsync(actor, error.Message, ct); }
        TempData["OwnLeaveSuccess"] = "درخواست مرخصی ثبت شد و در انتظار تأیید است.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelAsync(int requestId, string? version, CancellationToken ct)
    {
        var actor = await OwnerAsync(ct);
        if (actor is null) return Forbid();
        if (!ModelState.IsValid) { ModelState.Clear(); return await ErrorAsync(actor, "شناسهٔ درخواست معتبر نیست.", ct); }
        try { await leave.CancelOwnAsync(actor, requestId, Convert.FromBase64String(version ?? ""), ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (FormatException) { return await ErrorAsync(actor, "نسخهٔ درخواست معتبر نیست؛ صفحه را بازخوانی کنید.", ct); }
        catch (DbUpdateConcurrencyException) { return await ErrorAsync(actor, "درخواست تغییر کرده است؛ صفحه را بازخوانی کنید.", ct); }
        catch (LeaveBusyException error) { return await ErrorAsync(actor, error.Message, ct); }
        catch (ArgumentException error) { return await ErrorAsync(actor, error.Message, ct); }
        TempData["OwnLeaveSuccess"] = "درخواست مرخصی لغو شد.";
        return RedirectToPage();
    }

    private async Task<ActorScope?> OwnerAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        return actor is { IsActive: true, EmployeeId: > 0 } ? actor : null;
    }

    private async Task LoadAsync(ActorScope actor, int page, CancellationToken ct)
    {
        Employee = await employees.GetAsync(actor, actor.EmployeeId!.Value, ct);
        if (Employee is null) return;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
        CanSubmit = Employee.TerminationDate is null || Employee.TerminationDate > today;
        Policies = await leave.RequestPoliciesAsync(actor, ct);
        Pending = await leave.PendingOwnAsync(actor, page, ct);
    }

    private async Task<IActionResult> ErrorAsync(ActorScope actor, string message, CancellationToken ct)
    {
        ModelState.AddModelError("", message);
        await LoadAsync(actor, 1, ct);
        return Employee is null ? NotFound() : Page();
    }
}
