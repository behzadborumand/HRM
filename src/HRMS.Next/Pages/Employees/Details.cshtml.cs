using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Training;
using HRMS.Next.Modules.Documents;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages.Employees;

[Authorize]
public sealed class DetailsModel(AccessService access, EmployeeService employees, IOrganizationDirectory organization, DocumentService documents, EmployeeJobService jobs, EmployeeProfileDirectory directory, EmployeePhotoService photos, EmployeePayService pay, IEmployeeLeaveProfile leave, IEmployeeTrainingProfile training, TimeProvider clock) : PageModel
{
    public Employee? Employee { get; private set; }
    public bool CanWrite { get; private set; }
    public string? DepartmentName { get; private set; }
    public DocumentPage? Documents { get; private set; }
    public IReadOnlyList<JobHistoryRow> JobHistory { get; private set; } = [];
    public JobHistoryRow? CurrentJob => JobHistory.FirstOrDefault(x => x.IsCurrent);
    public PhotoSummary? Photo { get; private set; }
    public ProfileNavigation? Navigation { get; private set; }
    public DirectReportPage DirectReports { get; private set; } = new([], 0);
    public int EmploymentDays { get; private set; }
    public EmployeeLeaveProfile? LeaveProfile { get; private set; }
    public EmployeeTrainingProfile? TrainingProfile { get; private set; }
    public bool CanManageActivity { get; private set; }
    public bool CanViewPay { get; private set; }
    public IReadOnlyList<PayHistoryRow> PayHistory { get; private set; } = [];
    public static string PayTypeLabel(string? type) => type switch { "Salary" => "حقوق ثابت", "Hourly" => "ساعتی", _ => "—" };
    public string? CurrentJobTitle => JobHistory.FirstOrDefault(x => x.IsCurrent)?.JobTitle;
    public string? CurrentManagerName => JobHistory.FirstOrDefault(x => x.IsCurrent)?.ManagerName;
    public IReadOnlyDictionary<int, string> DepartmentNames { get; private set; } = new Dictionary<int, string>();
    public string DepartmentLabel(int? id) => id is { } value && DepartmentNames.TryGetValue(value, out var name) ? name : "-";
    public static string EmploymentTypeLabel(string? type) => type switch
    {
        "FullTime" => "تمام وقت", "PartTime" => "پاره وقت", "Contract" => "قراردادی", "Intern" => "کارآموز", _ => "-"
    };

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct, int leavePage = 1, int trainingPage = 1)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanRead(actor, actor.CompanyId)) return Forbid();
        Employee = await employees.GetAsync(actor, id, ct);
        CanWrite = AccessRules.CanWrite(actor, actor.CompanyId);
        if (Employee is not null)
        {
            DepartmentNames = (await organization.DepartmentsAsync(actor.CompanyId, ct))
                .ToDictionary(x => x.Id, x => x.Name);
            DepartmentName = DepartmentLabel(Employee.DepartmentId);
            JobHistory = await jobs.HistoryAsync(actor, id, ct);
            Documents = await documents.ListAsync(actor, id, 1, 20, ct);
            CanViewPay = AccessRules.CanReadPay(actor, actor.CompanyId, id);
            if (CanViewPay) PayHistory = await pay.HistoryAsync(actor, id, ct);
            CanManageActivity = actor.IsAdmin;
            if (AccessRules.CanReadEmployeeActivity(actor, actor.CompanyId, id))
            {
                LeaveProfile = await leave.ReadAsync(actor, id, leavePage, ct);
                TrainingProfile = await training.ReadAsync(actor, id, trainingPage, ct);
            }
            Photo = await photos.SummaryAsync(actor, id, ct);
            Navigation = await directory.NavigationAsync(actor, id, ct);
            DirectReports = await directory.DirectReportsAsync(actor, id, ct);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
            EmploymentDays = ProfileDisplay.EmploymentDays(Employee.HireDate, Employee.TerminationDate, today);
        }
        return Employee is null ? NotFound() : Page();
    }
}
