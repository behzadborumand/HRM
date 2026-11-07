using HRMS.Core.Services;

namespace HRMS.Core.Dtos
{
    public class TrainingAssignmentRow
    {
        public int AssignmentId { get; init; }
        public int EmployeeId { get; init; }
        public string EmployeeName { get; init; } = string.Empty;
        public string? DepartmentName { get; init; }
        public int CourseId { get; init; }
        public string CourseTitle { get; init; } = string.Empty;
        public string? CourseCategory { get; init; }
        public int? RenewalDays { get; init; }
        public DateTime AssignedDate { get; init; }
        public DateTime? DueDate { get; init; }
        public DateTime? CompletedDate { get; init; }
        public string? CertificateNumber { get; init; }
        public string? Notes { get; init; }
        public string? AssignedByName { get; init; }

        public string Status => TrainingRules.ResolveStatus(CompletedDate, DueDate, DateTime.Today);
        public int? DaysRemaining => TrainingRules.DaysRemaining(DueDate, DateTime.Today);

        /// <summary>وقتی دوره تکرارشدنی انجام شود، تاریخ بار دیگر باید آموزش دیده شود.</summary>
        public DateTime? RenewalDueDate => TrainingRules.NextDueDate(CompletedDate, RenewalDays);
    }

    public class TrainingCourseRow
    {
        public int CourseId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Category { get; init; }
        public string? Provider { get; init; }
        public string? Description { get; init; }
        public int? RenewalDays { get; init; }
        public bool MandatoryForAll { get; init; }
        public bool Active { get; init; }
        public DateTime CreatedDate { get; init; }
        public int AssignedCount { get; init; }
        public int OpenCount { get; init; }
    }
}
