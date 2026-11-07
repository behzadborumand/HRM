using HRMS.Core.Dtos;

namespace HRMS.Core.Repositories
{
    public interface IUserRepository
    {
        Task<AuthenticatedUser?> FindByUsernameAsync(string username);
        Task<UserListItem?> GetByEmployeeIdAsync(int employeeId);
        Task<List<UserListItem>> ListAsync();
        Task<bool> IsUsernameTakenAsync(string username, int? excludeUserId = null);
        Task<bool> HasAnyUserAsync();
    }

    public interface IReportingRepository
    {
        Task<DashboardStats> GetDashboardAsync();
    }
}
