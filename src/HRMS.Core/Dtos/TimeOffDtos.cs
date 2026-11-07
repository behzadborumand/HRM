namespace HRMS.Core.Dtos
{
    public class TimeOffRequestListItem
    {
        public int RequestId { get; init; }
        public int EmployeeId { get; init; }
        public string EmployeeName { get; init; } = string.Empty;
        public string? DepartmentName { get; init; }
        public int PolicyId { get; init; }
        public string PolicyName { get; init; } = string.Empty;
        public string? PolicyCategory { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public decimal? TotalHours { get; init; }
        public string? Reason { get; init; }
        public string Status { get; init; } = "Pending";
        public DateTime RequestedDate { get; init; }
        public string? RequestedBy { get; init; }
        public string? ApprovedBy { get; init; }
        public DateTime? ApprovedDate { get; init; }
        public string? Comments { get; init; }
    }

    public class TimeOffBalanceRow
    {
        public int BalanceId { get; init; }
        public int PolicyId { get; init; }
        public string PolicyName { get; init; } = string.Empty;
        public string? Category { get; init; }
        public int Year { get; init; }
        public decimal AccruedHours { get; init; }
        public decimal UsedHours { get; init; }
        public decimal PendingHours { get; init; }
        public decimal AvailableHours => AccruedHours - UsedHours;
        public bool Unlimited { get; init; }
    }

    public class TimeOffSummaryRow
    {
        public int EmployeeId { get; init; }
        public string EmployeeName { get; init; } = string.Empty;
        public string? DepartmentName { get; init; }
        public string PolicyName { get; init; } = string.Empty;
        public decimal AccruedHours { get; init; }
        public decimal UsedHours { get; init; }
        public decimal AvailableHours { get; init; }
        public int RequestCount { get; init; }
    }

    public class TimeOffStatusCount
    {
        public string Status { get; init; } = string.Empty;
        public int Count { get; init; }
        public decimal Hours { get; init; }
    }
}
