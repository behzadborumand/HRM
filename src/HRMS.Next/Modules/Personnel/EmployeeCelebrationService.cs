using System.Globalization;
using HRMS.Next.Modules.Access;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Personnel;

public enum CelebrationKind { Anniversary, Birthday }

//Date is the next public event occurrence, never the employee's original birth date.
public sealed record EmployeeCelebration(int EmployeeId, string FullName, DateOnly Date, CelebrationKind Kind, int? Years);

public sealed class EmployeeCelebrationService(PersonnelDbContext people)
{
    public async Task<IReadOnlyList<EmployeeCelebration>> LoadAsync(ActorScope actor, DateOnly today, CancellationToken ct)
    {
        if (actor.CompanyId <= 0 || !AccessRules.CanRead(actor, actor.CompanyId))
            throw new UnauthorizedAccessException();
        var calendar = new PersianCalendar();
        var minimum = DateOnly.FromDateTime(calendar.MinSupportedDateTime);
        if (today < minimum) return [];

        //Both sides are tenant scoped before projection. Raw birth dates stay inside Personnel;
        //no tracked private entity or private property is returned to Reporting/Razor.
        var candidates = from employee in people.Employees.AsNoTracking()
                         where employee.CompanyId == actor.CompanyId && employee.TerminationDate == null && employee.HireDate <= today
                         join personal in people.PersonalInfos.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId)
                             on employee.Id equals personal.EmployeeId into personalRows
                         from personal in personalRows.DefaultIfEmpty()
                         select new { employee.Id, employee.FirstName, employee.LastName, employee.HireDate,
                             BirthDate = personal == null ? (DateOnly?)null : personal.DateOfBirth };
        var rows = new List<EmployeeCelebration>();
        await foreach (var person in candidates.AsAsyncEnumerable().WithCancellation(ct))
        {
            if (person.BirthDate is { } birth && birth <= today && birth <= person.HireDate)
                Add(birth, CelebrationKind.Birthday);
            Add(person.HireDate, CelebrationKind.Anniversary);

            void Add(DateOnly original, CelebrationKind kind)
            {
                if (original < minimum || original > today) return;
                var originalDate = original.ToDateTime(TimeOnly.MinValue);
                var year = calendar.GetYear(today.ToDateTime(TimeOnly.MinValue));
                var month = calendar.GetMonth(originalDate);
                var day = calendar.GetDayOfMonth(originalDate);
                DateOnly Occurrence(int y) => DateOnly.FromDateTime(calendar.ToDateTime(y, month,
                    Math.Min(day, calendar.GetDaysInMonth(y, month)), 0, 0, 0, 0));
                DateOnly next;
                try
                {
                    next = Occurrence(year);
                    if (next < today) next = Occurrence(++year);
                }
                catch (ArgumentOutOfRangeException) { return; } //Calendar's final partial year.
                var years = year - calendar.GetYear(originalDate);
                if (next.DayNumber - today.DayNumber >= 30 || (kind == CelebrationKind.Anniversary && years < 1)) return;
                rows.Add(new(person.Id, person.FirstName + " " + person.LastName, next, kind,
                    kind == CelebrationKind.Anniversary ? years : null));
            }
        }
        return rows.OrderBy(x => x.Date).ThenBy(x => x.Kind)
            .ThenBy(x => x.FullName, StringComparer.Ordinal).ThenBy(x => x.EmployeeId).Take(6).ToList();
    }
}
