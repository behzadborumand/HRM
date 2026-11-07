using HRMS.Core.Dtos;
using HRMS.Core.Entities;

namespace HRMS.Web.ViewModels
{
    public class EmployeesReportViewModel
    {
        public List<EmployeeListItem> Rows { get; set; } = new();
        public string? Keyword { get; set; }
        public int? DepartmentId { get; set; }
        public string? Status { get; set; }
        public List<Department> Departments { get; set; } = new();
        public int TotalHeadcount => Rows.Count;
        public decimal TotalPayroll => Rows.Sum(r => r.PayRate) ?? 0;    }

    public class TimeOffReportViewModel
    {
        public List<TimeOffSummaryRow> Rows { get; set; } = new();
        public int Year { get; set; } = DateTime.Now.Year;
        public int? DepartmentId { get; set; }
        public List<Department> Departments { get; set; } = new();
        public decimal TotalAccrued => Rows.Sum(r => r.AccruedHours);
        public decimal TotalUsed => Rows.Sum(r => r.UsedHours);
        public decimal TotalAvailable => Rows.Sum(r => r.AvailableHours);
    }

    public class DashboardViewModel
    {
        public HRMS.Core.Dtos.DashboardStats Stats { get; set; } = new();
        public List<TimeOffRequestListItem> RecentRequests { get; set; } = new();
        public List<TimeOffRequestListItem> AwaitingMyApproval { get; set; } = new();
        public List<DashboardAbsenceDay> AbsenceDays { get; set; } = new();
        public List<DashboardShortcut> Shortcuts { get; set; } = new();
        public EmployeeProfile? CurrentEmployee { get; set; }
        public bool CanViewOrganization { get; set; }
        public bool CanBrowsePeople { get; set; }
        public IReadOnlyList<DashboardCelebration> Celebrations { get; set; } = Array.Empty<DashboardCelebration>();
        public IReadOnlyList<DashboardPerson> NewHires { get; set; } = Array.Empty<DashboardPerson>();
        public IReadOnlyList<OrganizationLink> OrganizationLinks { get; set; } = Array.Empty<OrganizationLink>();
        public int BalanceYear { get; set; } = DateTime.Today.Year;
        public List<TimeOffBalanceRow> MyBalances { get; set; } = new();
        public string EmployeeName { get; set; } = string.Empty;
    }

    public record DashboardAbsenceDay(DateTime Date, string Caption, IReadOnlyList<TimeOffRequestListItem> People);
    public record DashboardShortcut(string Title, string Description, string Icon, string Controller, string Action, int? EmployeeId = null);
}
