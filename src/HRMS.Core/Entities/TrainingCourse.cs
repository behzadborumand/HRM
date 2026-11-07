namespace HRMS.Core.Entities
{
    /// <summary>یک دوره آموزشی در کاتالوگ؛ RenewalDays دوره تکرار اجباری را مشخص می‌کند.</summary>
    public class TrainingCourse
    {
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Provider { get; set; }
        public string? Description { get; set; }
        public int? RenewalDays { get; set; }
        public bool MandatoryForAll { get; set; }
        public bool Active { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
