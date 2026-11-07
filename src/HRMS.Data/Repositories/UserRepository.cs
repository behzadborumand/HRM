using HRMS.Core.Dtos;
using HRMS.Core.Repositories;
using HRMS.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Data.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly HrmsDbContext _db;

        public UserRepository(HrmsDbContext db)
        {
            _db = db;
        }

        public async Task<AuthenticatedUser?> FindByUsernameAsync(string username)
        {
            var term = username.Trim();
            return await _db.Users.AsNoTracking()
                .Where(u => u.Username == term)
                .Select(u => new AuthenticatedUser
                {
                    UserId = u.UserId,
                    EmployeeId = u.EmployeeId,
                    Username = u.Username,
                    PasswordHash = u.PasswordHash,
                    IsActive = u.IsActive,
                    RoleName = _db.Roles.Where(r => r.RoleId == u.RoleId).Select(r => r.RoleName).FirstOrDefault()!,
                    Permissions = _db.Roles.Where(r => r.RoleId == u.RoleId).Select(r => r.Permissions).FirstOrDefault(),
                    DisplayName = _db.Employees.Where(e => e.EmployeeId == u.EmployeeId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefault()!
                })
                .FirstOrDefaultAsync();
        }

        public async Task<UserListItem?> GetByEmployeeIdAsync(int employeeId) =>
            await Rows().FirstOrDefaultAsync(u => u.EmployeeId == employeeId);

        public async Task<List<UserListItem>> ListAsync() => await Rows().OrderBy(u => u.Username).ToListAsync();

        public async Task<bool> IsUsernameTakenAsync(string username, int? excludeUserId = null)
        {
            var term = username.Trim();
            return await _db.Users.AsNoTracking().AnyAsync(u =>
                u.Username == term && (excludeUserId == null || u.UserId != excludeUserId));
        }

        public async Task<bool> HasAnyUserAsync() => await _db.Users.AnyAsync();

        private IQueryable<UserListItem> Rows()
        {
            return from user in _db.Users.AsNoTracking()
                   join employee in _db.Employees on user.EmployeeId equals employee.EmployeeId
                   join role in _db.Roles on user.RoleId equals role.RoleId
                   select new UserListItem
                   {
                       UserId = user.UserId,
                       Username = user.Username,
                       DisplayName = employee.FirstName + " " + employee.LastName,
                       RoleName = role.RoleName,
                       RoleId = role.RoleId,
                       EmployeeId = user.EmployeeId,
                       IsActive = user.IsActive,
                       LastLogin = user.LastLogin,
                       CreatedDate = user.CreatedDate
                   };
        }
    }
}
