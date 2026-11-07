namespace HRMS.Core.Services
{
    /// <summary>
    /// Training status is never stored: it is derived from the dates, so a record cannot say
    /// "Completed" with no completion date or "Pending" after its due date has passed.
    /// </summary>
    public static class TrainingRules
    {
        public const string StatusPending = "Pending";
        public const string StatusCompleted = "Completed";
        public const string StatusOverdue = "Overdue";

        public const int MaxTitleLength = 200;
        public const int MinRenewalDays = 1;
        public const int MaxRenewalDays = 3650;

        public static readonly string[] AllStatuses = { StatusPending, StatusCompleted, StatusOverdue };

        public static string ResolveStatus(DateTime? completedDate, DateTime? dueDate, DateTime today)
        {
            if (completedDate is not null)
            {
                return StatusCompleted;
            }

            return dueDate is not null && dueDate.Value.Date < today.Date ? StatusOverdue : StatusPending;
        }

        /// <summary>The date a renewed course falls due again; only completed renewals get one.</summary>
        public static DateTime? NextDueDate(DateTime? completedDate, int? renewalDays) =>
            completedDate is not null && renewalDays is > 0
                ? completedDate.Value.Date.AddDays(renewalDays.Value)
                : null;

        public static DateTime? DefaultDueDate(DateTime assignedDate, int? renewalDays) =>
            renewalDays is > 0 ? assignedDate.Date.AddDays(renewalDays.Value) : null;

        /// <summary>Negative when the due date has passed; null when the course has no deadline.</summary>
        public static int? DaysRemaining(DateTime? dueDate, DateTime today) =>
            dueDate is null ? null : (int)(dueDate.Value.Date - today.Date).TotalDays;

        public static string? ValidateCourse(string? title, int? renewalDays)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "عنوان دوره الزامی است.";
            }

            if (title.Trim().Length > MaxTitleLength)
            {
                return $"عنوان دوره نباید بیش از {MaxTitleLength} نویسه باشد.";
            }

            if (renewalDays is < MinRenewalDays)
            {
                return "دوره تکرار باید دست‌کم یک روز باشد.";
            }

            if (renewalDays > MaxRenewalDays)
            {
                return $"دوره تکرار نباید بیش از {MaxRenewalDays} روز باشد.";
            }

            return null;
        }

        public static string? ValidateAssignment(DateTime assignedDate, DateTime? dueDate, DateTime? completedDate, DateTime today)
        {
            if (dueDate is not null && dueDate.Value.Date < assignedDate.Date)
            {
                return "تاریخ سررسید نمی‌تواند پیش از تاریخ واگذاری باشد.";
            }

            if (completedDate is not null && completedDate.Value.Date < assignedDate.Date)
            {
                return "تاریخ انجام نمی‌تواند پیش از تاریخ واگذاری باشد.";
            }

            if (completedDate is not null && completedDate.Value.Date > today.Date)
            {
                return "تاریخ انجام نمی‌تواند مربوط به آینده باشد.";
            }

            return null;
        }
    }
}
