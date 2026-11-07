using HRMS.Core.Dtos;
using HRMS.Core.Paging;

namespace HRMS.Core.Repositories
{
    public interface IEmployeeRepository
    {
        Task<PagedResult<EmployeeListItem>> SearchAsync(string? keyword, int? departmentId, string? status, int page, int pageSize);
        Task<List<EmployeeListItem>> GetByIdsAsync(IReadOnlyCollection<int> employeeIds);
        Task<EmployeeProfile?> GetProfileAsync(int employeeId);
        Task<List<EmployeeLookup>> GetManagerCandidatesAsync(int? excludeEmployeeId = null);
        Task<List<EmployeeJobHistoryRow>> GetJobHistoryAsync(int employeeId);
        Task<List<EmployeeLookup>> GetDirectReportsAsync(int managerEmployeeId);
        Task<EmployeeNeighbours?> GetNeighboursAsync(int employeeId);
        Task<string> NextEmployeeNumberAsync();
        Task<bool> IsEmployeeNumberTakenAsync(string employeeNumber, int? excludeEmployeeId = null);
        Task<bool> IsEmailTakenAsync(string email, int? excludeEmployeeId = null);
    }
}
