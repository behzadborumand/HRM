using System.Globalization;

namespace HRMS.Web.Helpers
{
    public static class PersianDate
    {
        private static readonly PersianCalendar Jalali = new();

        public static string ToJalali(DateTime? date)
        {
            if (date is null || date.Value == default)
            {
                return "-";
            }

            return ToJalali(date.Value);
        }

        public static string ToJalali(DateTime date)
        {
            var year = Jalali.GetYear(date);
            var month = Jalali.GetMonth(date);
            var day = Jalali.GetDayOfMonth(date);
            return $"{year:0000}/{month:00}/{day:00}";
        }

        public static string ToJalaliInput(DateTime? date) =>
            date is null || date.Value == default ? string.Empty : ToJalali(date.Value);

        public static string ToJalaliInput(DateTime date) =>
            date == default ? string.Empty : ToJalali(date);

        /// <summary>Duration between two dates in Jalali years/months/days, e.g. the service length next to a hire date.</summary>
        public static string ToJalaliDuration(DateTime from, DateTime to)
        {
            if (to < from)
            {
                return "-";
            }

            var years = Jalali.GetYear(to) - Jalali.GetYear(from);
            var months = Jalali.GetMonth(to) - Jalali.GetMonth(from);
            var days = Jalali.GetDayOfMonth(to) - Jalali.GetDayOfMonth(from);

            if (days < 0)
            {
                months--;
                var previousMonth = Jalali.GetMonth(to) == 1 ? 12 : Jalali.GetMonth(to) - 1;
                var previousMonthYear = Jalali.GetMonth(to) == 1 ? Jalali.GetYear(to) - 1 : Jalali.GetYear(to);
                days += Jalali.GetDaysInMonth(previousMonthYear, previousMonth);
            }

            if (months < 0)
            {
                years--;
                months += 12;
            }

            var parts = new List<string>();
            if (years > 0)
            {
                parts.Add(ToPersianDigits($"{years}") + " سال");
            }

            if (months > 0)
            {
                parts.Add(ToPersianDigits($"{months}") + " ماه");
            }

            if (days > 0 || parts.Count == 0)
            {
                parts.Add(ToPersianDigits($"{days}") + " روز");
            }

            return string.Join(" - ", parts);
        }

        public static string ToJalaliWithGregorian(DateTime? date)
        {
            if (date is null || date.Value == default)
            {
                return "-";
            }

            return $"{ToJalali(date.Value)} ({date.Value:yyyy/MM/dd})";
        }

        public static string ToJalaliDateTime(DateTime? date)
        {
            if (date is null || date.Value == default)
            {
                return "-";
            }

            return $"{ToJalali(date.Value)} {date.Value:HH:mm}";
        }

        public static string ToPersianDigits(string value) =>
            string.Concat(value.Select(d => char.IsAsciiDigit(d) ? (char)('۰' + (d - '0')) : d));

        private static readonly Dictionary<char, char> PersianDigits = new()
        {
            ['۰'] = '0', ['۱'] = '1', ['۲'] = '2', ['۳'] = '3', ['۴'] = '4',
            ['۵'] = '5', ['۶'] = '6', ['۷'] = '7', ['۸'] = '8', ['۹'] = '9',
            ['٠'] = '0', ['١'] = '1', ['٢'] = '2', ['٣'] = '3', ['٤'] = '4',
            ['٥'] = '5', ['٦'] = '6', ['٧'] = '7', ['٨'] = '8', ['٩'] = '9'
        };

        // Jalali years overlap Gregorian only in fiction, so the year alone tells the two calendars apart.
        private const int JalaliYearLowerBound = 1200;
        private const int JalaliYearUpperBound = 1600;

        /// <summary>Accepts ۱۴۰۵/۰۴/۰۱, 1405-4-1 and an ISO Gregorian date alike.</summary>
        public static bool TryParse(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = new string(value.Trim()
                .Select(c => PersianDigits.TryGetValue(c, out var ascii) ? ascii : c)
                .ToArray());

            var parts = normalized.Split(['/', '-', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3 || parts[0].Length != 4 || !int.TryParse(parts[0], out var year)
                || !int.TryParse(parts[1], out var month) || !int.TryParse(parts[2], out var day))
            {
                return false;
            }

            if (year is >= JalaliYearLowerBound and <= JalaliYearUpperBound)
            {
                if (month is < 1 or > 12 || day is < 1 or > 31)
                {
                    return false;
                }

                try
                {
                    date = Jalali.ToDateTime(year, month, day, 0, 0, 0, 0);
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }
            }

            return DateTime.TryParseExact($"{year:0000}-{month:00}-{day:00}", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
