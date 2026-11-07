namespace HRMS.Core.Entities
{
    public class TimeOffRequest
    {
        public int RequestId { get; set; }
        public int EmployeeId { get; set; }
        public int PolicyId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal? TotalHours { get; set; }
        public string? Reason { get; set; }
        public string Status { get; set; } = "Pending";
        public int RequestedBy { get; set; }
        public int? ApprovedBy { get; set; }
        public DateTime RequestedDate { get; set; } = DateTime.Now;
        public DateTime? ApprovedDate { get; set; }
        public string? Comments { get; set; }
    }
}
