namespace HRMS.Next.Modules.Leave;

public static class LeaveRules
{
    public const decimal HoursPerWorkday = 8m;

    public static decimal WorkingHours(DateOnly start, DateOnly end)
    {
        if (end < start) return 0m;
        var days = end.DayNumber - start.DayNumber + 1;
        var working = (days / 7) * 6;
        for (var offset = 0; offset < days % 7; offset++)
        {
            if (((int)start.DayOfWeek + offset) % 7 != (int)DayOfWeek.Friday) working++;
        }
        return working * HoursPerWorkday;
    }

    public static decimal ProrateAccrual(DateOnly hireDate, int year, decimal annualHours)
    {
        if (annualHours <= 0) return 0m;
        if (hireDate <= new DateOnly(year, 1, 1)) return annualHours;
        if (hireDate > new DateOnly(year, 12, 31)) return 0m;
        var months = 13 - hireDate.Month;
        return Math.Round(annualHours * months / 12m, 2);
    }

    public static void ValidateRange(DateOnly start, DateOnly end, DateOnly hireDate, DateOnly? terminationDate)
    {
        if (end < start) throw new ArgumentException("End date precedes start date.");
        if (start < hireDate) throw new ArgumentException("Leave cannot start before hire date.");
        if (terminationDate is { } termination && end > termination)
            throw new ArgumentException("Leave cannot extend beyond termination date.");
        if (WorkingHours(start, end) <= 0) throw new ArgumentException("Leave range has no working day.");
    }
}