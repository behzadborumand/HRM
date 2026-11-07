using HRMS.Core.Dtos;
using HRMS.Core.Paging;

namespace HRMS.Core.Repositories
{
    public interface ITimeOffRepository
    {
        Task<PagedResult<TimeOffRequestListItem>> SearchAsync(int? employeeId, string? status, int page, int pageSize);
        Task<TimeOffRequestListItem?> GetAsync(int requestId);
        Task<List<TimeOffRequestListItem>> GetRecentForEmployeeAsync(int employeeId);
        Task<List<TimeOffRequestListItem>> GetPendingForManagerAsync(int managerEmployeeId);
        Task<List<TimeOffRequestListItem>> GetOutOnAsync(DateTime date, int? employeeId = null);
        Task<List<TimeOffBalanceRow>> GetBalancesAsync(int employeeId, int year);
        Task<List<TimeOffSummaryRow>> GetSummaryAsync(int year, int? departmentId);
        Task<List<TimeOffStatusCount>> GetStatusCountsAsync();
    }
}
