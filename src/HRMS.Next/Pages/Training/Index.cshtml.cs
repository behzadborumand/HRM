using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Modules.Training;
using HRMS.Next.Pages.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages.Training;

[Authorize]
public sealed class IndexModel(AccessService access, TrainingService service, EmployeeService employees) : PageModel
{
    [BindProperty] public string Title { get; set; } = "";
    [BindProperty] public int? RenewalDays { get; set; }
    [BindProperty] public int EmployeeId { get; set; }
    [BindProperty] public int CourseId { get; set; }
    [BindProperty] public string AssignedDateJalali { get; set; } = "";
    [BindProperty] public string? DueDateJalali { get; set; }
    [BindProperty] public int AssignmentId { get; set; }
    [BindProperty] public string CompletedDateJalali { get; set; } = "";
    [BindProperty] public string? CertificateNumber { get; set; }

    public IReadOnlyList<TrainingCourse> Courses { get; private set; } = [];
    public IReadOnlyList<TrainingAssignmentRow> Assignments { get; private set; } = [];
    public EmployeePage? EmployeeChoices { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        await LoadAsync(actor, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostCourseAsync(CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        try { await service.CreateCourseAsync(actor, Title, null, null, null, RenewalDays, false, ct); }
        catch (ArgumentException error) { return await ErrorAsync(actor, error.Message, ct); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAssignAsync(CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        if (!JalaliDate.TryParse(AssignedDateJalali, out var assigned))
            return await ErrorAsync(actor, "تاریخ واگذاری معتبر وارد کنید.", ct);
        DateOnly? due = null;
        if (!string.IsNullOrWhiteSpace(DueDateJalali))
        {
            if (!JalaliDate.TryParse(DueDateJalali, out var parsed))
                return await ErrorAsync(actor, "تاریخ سررسید معتبر وارد کنید.", ct);
            due = parsed;
        }
        try { await service.AssignAsync(actor, EmployeeId, CourseId, assigned, due, ct); }
        catch (ArgumentException error) { return await ErrorAsync(actor, error.Message, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCompleteAsync(CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        if (!JalaliDate.TryParse(CompletedDateJalali, out var completed))
            return await ErrorAsync(actor, "تاریخ انجام معتبر وارد کنید.", ct);
        try { await service.CompleteAsync(actor, AssignmentId, completed, CertificateNumber, ct); }
        catch (ArgumentException error) { return await ErrorAsync(actor, error.Message, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        return RedirectToPage();
    }

    private async Task<ActorScope?> AdminAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        return actor is { IsActive: true, IsAdmin: true } ? actor : null;
    }

    private async Task LoadAsync(ActorScope actor, CancellationToken ct)
    {
        Courses = await service.CoursesAsync(actor, ct);
        Assignments = await service.AssignmentsAsync(actor, ct);
        EmployeeChoices = await employees.SearchAsync(actor, null, null, 1, 100, ct);
    }

    private async Task<IActionResult> ErrorAsync(ActorScope actor, string message, CancellationToken ct)
    {
        ModelState.AddModelError("", message);
        await LoadAsync(actor, ct);
        return Page();
    }
}
