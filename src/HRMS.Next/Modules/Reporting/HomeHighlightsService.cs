using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Reporting;

public sealed record HomePerson(int EmployeeId, string FullName, string DepartmentName, DateOnly HireDate);
public sealed record HomeAbsenceDay(DateOnly Date, string Caption, IReadOnlyList<HomePerson> People);
public sealed record HomePendingRequest(int EmployeeId, string EmployeeName, string PolicyName, DateOnly StartDate, decimal Hours);
public sealed record HomeHighlights(IReadOnlyList<HomePerson> NewHires, IReadOnlyList<HomeAbsenceDay> AbsenceDays,
    IReadOnlyList<HomePendingRequest> PendingRequests, IReadOnlyList<EmployeeCelebration> Celebrations);

public sealed class HomeHighlightsService(PersonnelDbContext people, LeaveDbContext leave,
    OrganizationDbContext organization, TimeProvider clock, EmployeeCelebrationService celebrations)
{
    public async Task<HomeHighlights> LoadAsync(ActorScope actor, CancellationToken ct)
    {
        if (!AccessRules.CanRead(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
        var recent = await people.Employees.AsNoTracking()
            .Where(x => x.CompanyId == actor.CompanyId && x.TerminationDate == null &&
                x.HireDate >= today.AddDays(-30) && x.HireDate <= today)
            .OrderByDescending(x => x.HireDate).ThenBy(x => x.Id).Take(6)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.DepartmentId, x.HireDate }).ToListAsync(ct);
        var upcoming = await leave.Requests.AsNoTracking()
            .Where(x => x.CompanyId == actor.CompanyId && x.Status == "Approved" &&
                x.StartDate <= today.AddDays(2) && x.EndDate >= today)
            .Select(x => new { x.EmployeeId, x.StartDate, x.EndDate }).ToListAsync(ct);
        var pending = actor.IsAdmin
            ? await leave.Requests.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.Status == "Pending")
                .OrderBy(x => x.RequestedAtUtc).ThenBy(x => x.Id).Take(6)
                .Select(x => new { x.EmployeeId, x.PolicyId, x.StartDate, x.Hours }).ToListAsync(ct)
            : [];
        var ids = recent.Select(x => x.Id).Concat(upcoming.Select(x => x.EmployeeId))
            .Concat(pending.Select(x => x.EmployeeId)).Distinct().ToArray();
        var employeeRows = await people.Employees.AsNoTracking()
            .Where(x => x.CompanyId == actor.CompanyId && ids.Contains(x.Id))
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.DepartmentId, x.HireDate, x.TerminationDate })
            .ToListAsync(ct);
        var employees = employeeRows.ToDictionary(x => x.Id);
        var departmentIds = employeeRows.Select(x => x.DepartmentId).Distinct().ToArray();
        var departments = await organization.Departments.AsNoTracking()
            .Where(x => x.CompanyId == actor.CompanyId && departmentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var policyIds = pending.Select(x => x.PolicyId).Distinct().ToArray();
        var policies = await leave.Policies.AsNoTracking()
            .Where(x => x.CompanyId == actor.CompanyId && policyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        HomePerson? Person(int id)
        {
            if (!employees.TryGetValue(id, out var x)) return null;
            return new HomePerson(x.Id, x.FirstName + " " + x.LastName,
                departments.GetValueOrDefault(x.DepartmentId, "-"), x.HireDate);
        }
        var newHires = recent.Select(x => Person(x.Id)).OfType<HomePerson>().ToList();
        var captions = new[] { "امروز", "فردا", "پس‌فردا" };
        var days = Enumerable.Range(0, 3).Select(offset =>
        {
            var date = today.AddDays(offset);
            var persons = upcoming.Where(x => x.StartDate <= date && x.EndDate >= date)
                .Select(x => x.EmployeeId).Distinct().Select(Person).OfType<HomePerson>()
                .Where(x => employees[x.EmployeeId].TerminationDate == null).OrderBy(x => x.FullName).ToList();
            return new HomeAbsenceDay(date, captions[offset], persons);
        }).ToList();
        var pendingRows = pending.Select(x =>
        {
            var person = Person(x.EmployeeId);
            return person is null ? null : new HomePendingRequest(x.EmployeeId, person.FullName,
                policies.GetValueOrDefault(x.PolicyId, "-"), x.StartDate, x.Hours);
        }).OfType<HomePendingRequest>().ToList();
        return new HomeHighlights(newHires, days, pendingRows, await celebrations.LoadAsync(actor, today, ct));
    }
}
