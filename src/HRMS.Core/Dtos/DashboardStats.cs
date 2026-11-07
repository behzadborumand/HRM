namespace HRMS.Core.Dtos
{
    public class DashboardStats
    {
        public int TotalEmployees { get; init; }
        public int ActiveEmployees { get; init; }
        public int OnLeaveEmployees { get; init; }
        public int PendingRequests { get; init; }
        public int ApprovedRequestsThisMonth { get; init; }
        public int NewHiresThisYear { get; init; }
        public int DepartmentCount { get; init; }
        public decimal AverageTeamSize { get; init; }
        public IReadOnlyList<DepartmentHeadcount> HeadcountByDepartment { get; init; } = Array.Empty<DepartmentHeadcount>();
        public IReadOnlyList<TimeOffStatusCount> RequestStatusBreakdown { get; init; } = Array.Empty<TimeOffStatusCount>();
    }

    public class DepartmentHeadcount
    {
        public string DepartmentName { get; init; } = string.Empty;
        public int Headcount { get; init; }
        public decimal PayrollCost { get; init; }
    }
}
