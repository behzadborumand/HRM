using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages.Reports;

[Authorize]
public sealed class LeaveModel(AccessService access, ReportingService reports, IOrganizationDirectory organization) : PageModel
{
    public int Year { get; private set; }
    public int? DepartmentId { get; private set; }
    public LeaveReportPage? Result { get; private set; }
    public IReadOnlyList<DepartmentOption> Departments { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int? year, int? departmentId, int pageNumber = 1, CancellationToken ct = default)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        Year = year ?? DateTime.UtcNow.Year;
        DepartmentId = departmentId;
        Departments = await organization.DepartmentsAsync(actor.CompanyId, ct);
        try { Result = await reports.LeaveSummaryAsync(actor, Year, DepartmentId, pageNumber, 20, ct); }
        catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(int? year, int? departmentId, CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        var selectedYear = year ?? DateTime.UtcNow.Year;
        if (selectedYear is < 2015 or > 2100) return BadRequest("سال گزارش معتبر نیست.");
        var rows = new List<string[]>();
        for (var page = 1; page <= 5; page++)
        {
            var result = await reports.LeaveSummaryAsync(actor, selectedYear, departmentId, page, 100, ct);
            rows.AddRange(result.Rows.Select(x => new[] { x.EmployeeName, x.DepartmentName, x.PolicyName,
                x.AccruedHours.ToString("0.##"), x.UsedHours.ToString("0.##"), x.AvailableHours.ToString("0.##"), x.RequestCount.ToString() }));
            if (rows.Count >= Math.Min(result.Total, 500)) break;
        }
        var bytes = CsvReport.ToUtf8(["کارمند", "واحد سازمانی", "نوع مرخصی", "ساعت تعلق‌گرفته", "ساعت استفاده‌شده", "مانده", "تعداد درخواست"], rows);
        return File(bytes, "text/csv; charset=utf-8", $"morkhasi-{selectedYear}.csv");
    }

    private async Task<ActorScope?> AdminAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        return actor is { IsActive: true, IsAdmin: true } ? actor : null;
    }
}
