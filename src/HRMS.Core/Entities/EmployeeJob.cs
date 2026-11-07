namespace HRMS.Core.Entities
{
    public class EmployeeJob
    {
        public int EmployeeJobId { get; set; }
        public int EmployeeId { get; set; }
        public int? JobTitleId { get; set; }
        public int? DepartmentId { get; set; }
        public int? ManagerId { get; set; }
        public string? EmploymentType { get; set; }
        public string? PayType { get; set; }
        public decimal? PayRate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsCurrent { get; set; } = true;
    }
}
