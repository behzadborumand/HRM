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
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DetailsModel(AccessService access, EmployeeService employees, IOrganizationDirectory organization, DocumentService documents, EmployeeJobService jobs, EmployeeProfileDirectory directory, EmployeePhotoService photos, EmployeePayService pay, EmployeePersonalService personal, IEmployeeLeaveProfile leave, IEmployeeTrainingProfile training, TimeProvider clock, EmployeeEmploymentService employment) : PageModel
{
    public EmployeePersonalInfo? Personal {get;private set;}
    public bool CanViewPersonal {get;private set;}
    public bool CanEditPersonal {get;private set;}
    public Employee? Employee { get; private set; }
    public bool CanViewDocuments { get; private set; }
    public bool CanUseTeam { get; private set; }
    public bool CanWrite { get; private set; }
    public bool CanBrowseEmployees { get; private set; }
    public bool IsOwnProfile { get; private set; }
    public string? DepartmentName { get; private set; }
    public DocumentPage? Documents { get; private set; }
    public IReadOnlyList<EmploymentStatusRow> StatusHistory { get; private set; } = [];
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
        if (actor is null) return Forbid();
        try { Employee = await employees.GetAsync(actor, id, ct); } catch (UnauthorizedAccessException) { return Forbid(); }
        CanWrite = AccessRules.CanWrite(actor, actor.CompanyId);
        CanBrowseEmployees = AccessRules.CanRead(actor, actor.CompanyId);
        IsOwnProfile = actor.EmployeeId == id;
        CanUseTeam = AccessRules.CanUseTeam(actor);
        if (Employee is not null)
        {
            CanViewPersonal=EmployeePersonalService.CanRead(actor,id);
            CanEditPersonal=CanViewPersonal&&(actor.IsAdmin||Employee.TerminationDate is null);
            if(CanViewPersonal)Personal=await personal.ReadAsync(actor,id,ct);
            DepartmentNames = (await organization.DepartmentsAsync(actor.CompanyId, ct))
                .ToDictionary(x => x.Id, x => x.Name);
            DepartmentName = DepartmentLabel(Employee.DepartmentId);
            JobHistory = await jobs.HistoryAsync(actor, id, ct);
            StatusHistory = await employment.HistoryAsync(actor, id, ct);
            CanViewDocuments = AccessRules.CanReadEmployee(actor, actor.CompanyId, id);
            if (CanViewDocuments) Documents = await documents.ListAsync(actor, id, 1, 20, ct);
            CanViewPay = AccessRules.CanReadPay(actor, actor.CompanyId, id);
            if (CanViewPay) PayHistory = await pay.HistoryAsync(actor, id, ct);
            CanManageActivity = actor.IsAdmin;
            if (AccessRules.CanReadEmployeeActivity(actor, actor.CompanyId, id))
            {
                LeaveProfile = await leave.ReadAsync(actor, id, leavePage, ct);
                TrainingProfile = await training.ReadAsync(actor, id, trainingPage, ct);
            }
            Photo = await photos.SummaryAsync(actor, id, ct);
            if (CanBrowseEmployees)
            {
                Navigation = await directory.NavigationAsync(actor, id, ct);
                DirectReports = await directory.DirectReportsAsync(actor, id, ct);
            }
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
            EmploymentDays = ProfileDisplay.EmploymentDays(Employee.HireDate, Employee.TerminationDate, today);
        }
        return Employee is null ? NotFound() : Page();
    }
}
