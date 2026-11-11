using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Reporting;

public sealed record ReportingDashboard(int TotalEmployees, int ActiveEmployees, int OnLeaveEmployees,
    int PendingRequests, int NewHiresThisYear, int DepartmentCount, decimal AverageTeamSize);
public sealed record EmployeeReportRow(int EmployeeId, string EmployeeNumber, string FullName,
    string? Email, string DepartmentName, string? JobTitle, string? ManagerName, DateOnly HireDate, bool Terminated);
public sealed record EmployeeReportPage(IReadOnlyList<EmployeeReportRow> Rows, int Total, int Page, int PageSize);
public sealed record LeaveReportRow(int EmployeeId, string EmployeeName, string DepartmentName,
    string PolicyName, decimal AccruedHours, decimal UsedHours, decimal AvailableHours, int RequestCount);
public sealed record LeaveReportPage(IReadOnlyList<LeaveReportRow> Rows, int Total, int Page, int PageSize);

public sealed class ReportingService(PersonnelDbContext people, LeaveDbContext leave,
    OrganizationDbContext organization, TimeProvider clock)
{
    public async Task<ReportingDashboard> DashboardAsync(ActorScope actor, CancellationToken ct)
    {
        RequireAdmin(actor);
        var today = TodayInIran();
        var employees = people.Employees.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId);
        var total = await employees.CountAsync(ct);
        var active = await employees.CountAsync(x => x.TerminationDate == null, ct);
        var onLeave = await leave.Requests.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId &&
            x.Status == "Approved" && x.StartDate <= today && x.EndDate >= today)
            .Select(x => x.EmployeeId).Distinct().CountAsync(ct);
        var pending = await leave.Requests.AsNoTracking().CountAsync(x => x.CompanyId == actor.CompanyId && x.Status == "Pending", ct);
        var hires = await employees.CountAsync(x => x.HireDate >= new DateOnly(today.Year, 1, 1) && x.HireDate <= today, ct);
        var departmentCount = await organization.Departments.AsNoTracking().CountAsync(x => x.CompanyId == actor.CompanyId && x.IsActive, ct);
        var headcounts = await employees.Where(x => x.TerminationDate == null).GroupBy(x => x.DepartmentId)
            .Select(x => x.Count()).ToListAsync(ct);
        var average = headcounts.Count == 0 ? 0 : Math.Round((decimal)headcounts.Average(), 1);
        return new ReportingDashboard(total, active, onLeave, pending, hires, departmentCount, average);
    }

    public async Task<EmployeeReportPage> EmployeesAsync(ActorScope actor, string? keyword, int? departmentId,
        string? status, int page, int pageSize, CancellationToken ct)
    {
        RequireAdmin(actor);
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);
        var query = people.Employees.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x => x.EmployeeNumber.Contains(term) || x.FirstName.Contains(term) ||
                x.LastName.Contains(term) || (x.Email != null && x.Email.Contains(term)));
        }
        if (departmentId is > 0) query = query.Where(x => x.DepartmentId == departmentId);
        if (status == "Active") query = query.Where(x => x.TerminationDate == null);
        if (status == "Terminated") query = query.Where(x => x.TerminationDate != null);
        if (status == "OnLeave")
        {
            var today = TodayInIran();
            var onLeaveIds = await leave.Requests.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId &&
                x.Status == "Approved" && x.StartDate <= today && x.EndDate >= today)
                .Select(x => x.EmployeeId).Distinct().ToListAsync(ct);
            query = query.Where(x => onLeaveIds.Contains(x.Id));
        }
        var total = await query.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (total + pageSize - 1) / pageSize));
        var selected = await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.EmployeeNumber, x.FirstName, x.LastName, x.Email, x.DepartmentId, x.HireDate, x.TerminationDate })
            .ToListAsync(ct);
        var employeeIds = selected.Select(x => x.Id).ToArray();
        var departmentIds = selected.Select(x => x.DepartmentId).Distinct().ToArray();
        var departmentNames = await organization.Departments.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && departmentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var jobs = await people.EmployeeJobs.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.IsCurrent && employeeIds.Contains(x.EmployeeId))
            .Select(x => new { x.EmployeeId, x.JobTitle, x.ManagerEmployeeId }).ToListAsync(ct);
        var jobByEmployee = jobs.ToDictionary(x => x.EmployeeId);
        var managerIds = jobs.Where(x => x.ManagerEmployeeId != null).Select(x => x.ManagerEmployeeId!.Value).Distinct().ToArray();
        var managers = await people.Employees.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && managerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FirstName + " " + x.LastName, ct);
        var rows = selected.Select(x =>
        {
            jobByEmployee.TryGetValue(x.Id, out var job);
            return new EmployeeReportRow(x.Id, x.EmployeeNumber, x.FirstName + " " + x.LastName,
                x.Email, departmentNames.GetValueOrDefault(x.DepartmentId, "-"), job?.JobTitle,
                job?.ManagerEmployeeId is { } managerId ? managers.GetValueOrDefault(managerId) : null,
                x.HireDate, x.TerminationDate != null);
        }).ToList();
        return new EmployeeReportPage(rows, total, page, pageSize);
    }

    public async Task<LeaveReportPage> LeaveSummaryAsync(ActorScope actor, int year, int? departmentId,
        int page, int pageSize, CancellationToken ct)
    {
        RequireAdmin(actor);
        if (year is < 2015 or > 2100) throw new ArgumentException("سال گزارش معتبر نیست.");
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);
        IQueryable<LeaveBalance> balances = departmentId is > 0
            ? leave.Balances.FromSqlInterpolated($"SELECT b.* FROM [leave].[Balances] b JOIN [personnel].[Employees] e ON e.Id=b.EmployeeId AND e.CompanyId=b.CompanyId WHERE e.DepartmentId={departmentId}")
            : leave.Balances;
        balances = balances.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.Year == year);
        var total = await balances.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (total + pageSize - 1) / pageSize));
        var selected = await balances.OrderBy(x => x.EmployeeId).ThenBy(x => x.PolicyId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.EmployeeId, x.PolicyId, x.AccruedHours, x.UsedHours }).ToListAsync(ct);
        var employeeIds = selected.Select(x => x.EmployeeId).Distinct().ToArray();
        var policyIds = selected.Select(x => x.PolicyId).Distinct().ToArray();
        var employees = await people.Employees.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && employeeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.DepartmentId }).ToListAsync(ct);
        var employeeById = employees.ToDictionary(x => x.Id);
        var departmentIds = employees.Select(x => x.DepartmentId).Distinct().ToArray();
        var departments = await organization.Departments.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && departmentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var policies = await leave.Policies.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && policyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var requests = await leave.Requests.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId &&
            employeeIds.Contains(x.EmployeeId) && policyIds.Contains(x.PolicyId) && x.StartDate.Year == year)
            .GroupBy(x => new { x.EmployeeId, x.PolicyId }).Select(x => new { x.Key.EmployeeId, x.Key.PolicyId, Count = x.Count() })
            .ToListAsync(ct);
        var requestCounts = requests.ToDictionary(x => (x.EmployeeId, x.PolicyId), x => x.Count);
        var rows = selected.Select(x =>
        {
            employeeById.TryGetValue(x.EmployeeId, out var employee);
            return new LeaveReportRow(x.EmployeeId,
                employee is null ? "-" : employee.FirstName + " " + employee.LastName,
                employee is null ? "-" : departments.GetValueOrDefault(employee.DepartmentId, "-"),
                policies.GetValueOrDefault(x.PolicyId, "-"), x.AccruedHours, x.UsedHours,
                x.AccruedHours - x.UsedHours, requestCounts.GetValueOrDefault((x.EmployeeId, x.PolicyId)));
        }).ToList();
        return new LeaveReportPage(rows, total, page, pageSize);
    }

    private DateOnly TodayInIran() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
        TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);

    private static void RequireAdmin(ActorScope actor)
    {
        if (actor is not { IsActive: true, IsAdmin: true } || actor.CompanyId <= 0)
            throw new UnauthorizedAccessException();
    }
}
