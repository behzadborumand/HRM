namespace HRMS.Core.Services
{
    /// <summary>Shared leave-accrual rules used by both seeding and the running application.</summary>
    public static class TimeOffRules
    {
        public const decimal HoursPerWorkday = 8m;

        /// <summary>Iranian working week: Friday is the day off.</summary>
        public static bool IsWorkingDay(DateTime date) => date.DayOfWeek != DayOfWeek.Friday;

        public static int WorkingDaysBetween(DateTime start, DateTime end)
        {
            if (end < start)
            {
                return 0;
            }

            var count = 0;
            for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
            {
                if (IsWorkingDay(day))
                {
                    count++;
                }
            }

            return count;
        }

        public static decimal WorkingHours(DateTime start, DateTime end) =>
            WorkingDaysBetween(start, end) * HoursPerWorkday;

        /// <summary>Full entitlement when the employee started before the year, otherwise pro-rated by whole months of service.</summary>
        public static decimal ProrateAccrual(DateTime hireDate, int year, decimal annualHours)
        {
            if (annualHours <= 0)
            {
                return 0;
            }

            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = new DateTime(year, 12, 31);

            if (hireDate <= yearStart)
            {
                return annualHours;
            }

            if (hireDate > yearEnd)
            {
                return 0;
            }

            var monthsOfService = (yearEnd.Year - hireDate.Year) * 12 + (yearEnd.Month - hireDate.Month) + 1;
            return Math.Round(annualHours * monthsOfService / 12m, 2);
        }
    }
}
