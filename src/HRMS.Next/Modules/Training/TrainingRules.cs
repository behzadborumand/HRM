namespace HRMS.Next.Modules.Training;

public static class TrainingRules
{
    public static string ResolveStatus(DateOnly? completed, DateOnly? due, DateOnly today) =>
        completed is not null ? "Completed" : due is not null && due.Value < today ? "Overdue" : "Pending";

    public static DateOnly? NextDueDate(DateOnly? completed, int? renewalDays) =>
        completed is not null && renewalDays is > 0 ? completed.Value.AddDays(renewalDays.Value) : null;

    public static string ValidateCourse(string? title, int? renewalDays)
    {
        title = title?.Trim() ?? "";
        if (title.Length is 0 or > 200) throw new ArgumentException("Course title is required and must fit 200 characters.");
        if (renewalDays is < 1 or > 3650) throw new ArgumentException("Renewal days must be between 1 and 3650.");
        return title;
    }

    public static void ValidateAssignment(DateOnly assigned, DateOnly? due, DateOnly? completed, DateOnly today)
    {
        if (due < assigned) throw new ArgumentException("Due date precedes assignment.");
        if (completed < assigned || completed > today) throw new ArgumentException("Completion date is outside the valid range.");
    }
}
