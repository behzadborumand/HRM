using HRMS.Next.Modules.Access;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Personnel;

public sealed record ProfileNavigation(int Position, int Total, int? PreviousId, int? NextId);
public sealed record DirectReportRow(int Id, string FullName, string? JobTitle);
public sealed record DirectReportPage(IReadOnlyList<DirectReportRow> Rows, int Total);

public sealed class EmployeeProfileDirectory(PersonnelDbContext db)
{
    public async Task<ProfileNavigation?> NavigationAsync(ActorScope actor, int employeeId, CancellationToken ct)
    {
        RequireRead(actor);
        var employees = db.Employees.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId);
        var person = await employees.Where(x => x.Id == employeeId)
            .Select(x => new { x.Id, x.LastName, x.FirstName }).SingleOrDefaultAsync(ct);
        if (person is null) return null;
        var before = employees.Where(x => x.LastName.CompareTo(person.LastName) < 0 ||
            (x.LastName == person.LastName && x.FirstName.CompareTo(person.FirstName) < 0) ||
            (x.LastName == person.LastName && x.FirstName == person.FirstName && x.Id < person.Id));
        var after = employees.Where(x => x.LastName.CompareTo(person.LastName) > 0 ||
            (x.LastName == person.LastName && x.FirstName.CompareTo(person.FirstName) > 0) ||
            (x.LastName == person.LastName && x.FirstName == person.FirstName && x.Id > person.Id));
        var total = await employees.CountAsync(ct);
        var position = await before.CountAsync(ct) + 1;
        var previous = await before.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName)
            .ThenByDescending(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        var next = await after.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .ThenBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        return new ProfileNavigation(position, total, previous, next);
    }

    public async Task<DirectReportPage> DirectReportsAsync(ActorScope actor, int managerId, CancellationToken ct)
    {
        RequireRead(actor);
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.Id == managerId && x.CompanyId == actor.CompanyId, ct))
            return new DirectReportPage([], 0);
        var query = from employee in db.Employees.AsNoTracking()
                    join job in db.EmployeeJobs.AsNoTracking() on employee.Id equals job.EmployeeId
                    where employee.CompanyId == actor.CompanyId && job.CompanyId == actor.CompanyId &&
                        job.IsCurrent && job.ManagerEmployeeId == managerId && employee.TerminationDate == null
                    select new { employee.Id, employee.FirstName, employee.LastName, job.JobTitle };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Id).Take(6)
            .Select(x => new DirectReportRow(x.Id, x.FirstName + " " + x.LastName, x.JobTitle)).ToListAsync(ct);
        return new DirectReportPage(rows, total);
    }

    private static void RequireRead(ActorScope actor)
    {
        if (!AccessRules.CanRead(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
    }
}
