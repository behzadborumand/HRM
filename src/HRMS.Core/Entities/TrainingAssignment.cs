namespace HRMS.Core.Entities
{
    /// <summary>ثبت آموزش یک کارمند در یک دوره. وضعیت در دیتابیس ذخیره نمی‌شود و از dates ساخته می‌شود.</summary>
    public class TrainingAssignment
    {
        public int AssignmentId { get; set; }
        public int EmployeeId { get; set; }
        public int CourseId { get; set; }
        public DateTime AssignedDate { get; set; } = DateTime.Today;
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string? CertificateNumber { get; set; }
        public string? Notes { get; set; }
        public int? AssignedBy { get; set; }
    }
}
