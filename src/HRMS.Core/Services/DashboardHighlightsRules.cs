using HRMS.Core.Dtos;
using System.Globalization;

namespace HRMS.Core.Services;

public static class DashboardHighlightsRules
{
    public static IReadOnlyList<DashboardCelebration> Celebrations(IEnumerable<DashboardPerson> people, DateTime today)
    {
        today = today.Date;
        var calendar = new PersianCalendar();
        var rows = new List<DashboardCelebration>();
        foreach (var person in people.Where(p => p.HireDate.Date <= today))
        {
            if (person.DateOfBirth is DateTime birth && birth.Date <= today)
                Add(person, birth, "Birthday");
            Add(person, person.HireDate, "Anniversary");
        }
        return rows.OrderBy(r => r.Date).ThenBy(r => r.Kind).ThenBy(r => r.Person.FullName)
            .ThenBy(r => r.Person.EmployeeId).Take(6).ToList();

        void Add(DashboardPerson person, DateTime original, string kind)
        {
            var year = calendar.GetYear(today);
            var month = calendar.GetMonth(original);
            var day = calendar.GetDayOfMonth(original);
            DateTime InYear(int y) => calendar.ToDateTime(y, month, Math.Min(day, calendar.GetDaysInMonth(y, month)), 0, 0, 0, 0);
            var date = InYear(year);
            if (date < today) date = InYear(++year);
            var years = year - calendar.GetYear(original);
            if (date >= today.AddDays(30) || (kind == "Anniversary" && years < 1)) return;
            rows.Add(new(person, date, kind, kind == "Anniversary" ? years : null));
        }
    }

    public static IReadOnlyList<DashboardPerson> NewHires(IEnumerable<DashboardPerson> people, DateTime today) =>
        people.Where(p => p.HireDate.Date >= today.Date.AddDays(-29) && p.HireDate.Date <= today.Date)
            .OrderByDescending(p => p.HireDate).ThenBy(p => p.EmployeeId).Take(3).ToList();
}
