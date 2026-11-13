using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Reporting;
using HRMS.Next.Pages.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages.Reports;

[Authorize]
public sealed class EmployeesModel(AccessService access, ReportingService reports, IOrganizationDirectory organization) : PageModel
{
    public string? Keyword { get; private set; }
    public int? DepartmentId { get; private set; }
    public string? Status { get; private set; }
    public EmployeeReportPage? Result { get; private set; }
    public IReadOnlyList<DepartmentOption> Departments { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string? keyword, int? departmentId, string? status, int pageNumber = 1, CancellationToken ct = default)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        Keyword = keyword; DepartmentId = departmentId; Status = NormalizeStatus(status);
        Departments = await organization.DepartmentsAsync(actor.CompanyId, ct);
        Result = await reports.EmployeesAsync(actor, Keyword, DepartmentId, Status, pageNumber, 20, ct);
        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(string? keyword, int? departmentId, string? status, CancellationToken ct)
    {
        var actor = await AdminAsync(ct);
        if (actor is null) return Forbid();
        var rows = new List<string[]>();
        for (var page = 1; page <= 5; page++)
        {
            var result = await reports.EmployeesAsync(actor, keyword, departmentId, NormalizeStatus(status), page, 100, ct);
            rows.AddRange(result.Rows.Select(x => new[] { x.EmployeeNumber, x.FullName, x.JobTitle ?? "", x.DepartmentName,
                x.ManagerName ?? "", x.Email ?? "", JalaliDate.Format(x.HireDate), ProfileDisplay.EmploymentStatusLabel(x.Terminated, x.IsOnLeave) }));
            if (rows.Count >= Math.Min(result.Total, 500)) break;
        }
        var bytes = CsvReport.ToUtf8(["کد پرسنلی", "نام و نام خانوادگی", "عنوان شغلی", "واحد سازمانی", "مدیر مستقیم", "ایمیل", "تاریخ استخدام", "وضعیت"], rows);
        return File(bytes, "text/csv; charset=utf-8", $"karmanan-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private async Task<ActorScope?> AdminAsync(CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        return actor is { IsActive: true, IsAdmin: true } ? actor : null;
    }
    private static string? NormalizeStatus(string? status) => status is "Active" or "Terminated" or "OnLeave" ? status : null;
}
