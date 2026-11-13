using System.Globalization;

namespace HRMS.Next.Pages.Employees;

public static class ProfileDisplay
{
    public static string EmploymentStatusLabel(bool terminated, bool onLeave) => terminated ? "خروج قطعی" : onLeave ? "در مرخصی" : "فعال";

    public static string PayRate(decimal? rate) => rate?.ToString("#,0.##", CultureInfo.InvariantCulture) ?? "—";
    public static int EmploymentDays(DateOnly hireDate, DateOnly? terminationDate, DateOnly today) =>
        Math.Max(0, Math.Min(today.DayNumber, terminationDate?.DayNumber ?? today.DayNumber) - hireDate.DayNumber);
}
