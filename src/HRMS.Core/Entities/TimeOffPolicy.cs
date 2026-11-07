namespace HRMS.Core.Entities
{
    public class TimeOffPolicy
    {
        public int PolicyId { get; set; }
        public string PolicyName { get; set; } = string.Empty;
        public int? CompanyId { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public decimal AnnualHours { get; set; }
        public bool Active { get; set; } = true;
    }
}
