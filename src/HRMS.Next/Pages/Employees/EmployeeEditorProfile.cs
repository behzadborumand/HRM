using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;

namespace HRMS.Next.Pages.Employees;

//Presentation context only; never includes salary or private PersonalInfos.
public sealed record EmployeeEditorProfile(Employee Employee, JobHistoryRow? CurrentJob, string DepartmentName,
    PhotoSummary? Photo, int EmploymentDays, bool CanBrowseEmployees, bool CanWrite,
    bool CanViewPersonal, bool CanViewPay, bool CanViewActivity, bool IsPersonalEditor);

public sealed class EmployeeEditorProfileLoader(EmployeeJobService jobs, EmployeePhotoService photos,
    IOrganizationDirectory organization, TimeProvider clock)
{
    public async Task<EmployeeEditorProfile> LoadAsync(ActorScope actor, Employee employee, bool personalEditor, CancellationToken ct)
    {
        if (!AccessRules.CanReadEmployee(actor, employee.CompanyId, employee.Id))
            throw new UnauthorizedAccessException();
        var departments = await organization.DepartmentsAsync(actor.CompanyId, ct);
        var current = (await jobs.HistoryAsync(actor, employee.Id, ct)).FirstOrDefault(x => x.IsCurrent);
        var photo = await photos.SummaryAsync(actor, employee.Id, ct);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
        return new(employee, current, departments.FirstOrDefault(x => x.Id == employee.DepartmentId)?.Name ?? "—",
            photo, ProfileDisplay.EmploymentDays(employee.HireDate, employee.TerminationDate, today),
            AccessRules.CanRead(actor, actor.CompanyId), AccessRules.CanWrite(actor, actor.CompanyId),
            EmployeePersonalService.CanRead(actor, employee.Id), AccessRules.CanReadPay(actor, actor.CompanyId, employee.Id),
            AccessRules.CanReadEmployeeActivity(actor, actor.CompanyId, employee.Id), personalEditor);
    }
}
