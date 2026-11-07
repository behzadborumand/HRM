using HRMS.Core.Dtos;
using HRMS.Core.Repositories;
using HRMS.Core.Security;
using HRMS.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Web.Services
{
    public class AccountService
    {
        private readonly IUserRepository _users;
        private readonly IRepository<Core.Entities.User> _store;
        private readonly IRepository<Core.Entities.Role> _roles;

        public AccountService(IUserRepository users, IRepository<Core.Entities.User> store, IRepository<Core.Entities.Role> roles)
        {
            _users = users;
            _store = store;
            _roles = roles;
        }

        public async Task<AuthenticatedUser?> ValidateAsync(string username, string password)
        {
            var user = await _users.FindByUsernameAsync(username);
            if (user is null || !user.IsActive || !PasswordHasher.Verify(password, user.PasswordHash))
            {
                return null;
            }

            var entity = await _store.FindAsync(user.UserId);
            if (entity is not null)
            {
                entity.LastLogin = DateTime.Now;
                _store.Update(entity);
                await _store.SaveChangesAsync();
            }

            return user;
        }

        public async Task<OperationResult> CreateAsync(string username, int employeeId, int roleId, string password, bool isActive = true)
        {
            username = username.Trim();

            if (await _users.IsUsernameTakenAsync(username))
            {
                return OperationResult.Fail($"نام کاربری {username} قبلاً ثبت شده است.");
            }

            if (password.Length < 8)
            {
                return OperationResult.Fail("رمزعبور حداقل ۸ نویسه است.");
            }

            if (!await _roles.Query().AnyAsync(r => r.RoleId == roleId))
            {
                return OperationResult.Fail("نقش انتخاب‌شده معتبر نیست.");
            }

            if (await _store.Query().AnyAsync(u => u.EmployeeId == employeeId))
            {
                return OperationResult.Fail("این کارمند قبلاً حساب کاربری دارد.");
            }

            await _store.AddAsync(new Core.Entities.User
            {
                Username = username,
                EmployeeId = employeeId,
                RoleId = roleId,
                PasswordHash = PasswordHasher.Hash(password),
                IsActive = isActive,
                CreatedDate = DateTime.Now
            });
            await _store.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> UpdateAsync(int userId, string username, int employeeId, int roleId, string? password, bool isActive)
        {
            var user = await _store.FindAsync(userId);
            if (user is null)
            {
                return OperationResult.Fail("کاربر مورد نظر یافت نشد.");
            }

            username = username.Trim();
            if (await _users.IsUsernameTakenAsync(username, userId))
            {
                return OperationResult.Fail($"نام کاربری {username} قبلاً ثبت شده است.");
            }

            if (!await _roles.Query().AnyAsync(r => r.RoleId == roleId))
            {
                return OperationResult.Fail("نقش انتخاب‌شده معتبر نیست.");
            }

            if (await _store.Query().AnyAsync(u => u.EmployeeId == employeeId && u.UserId != userId))
            {
                return OperationResult.Fail("این کارمند حساب کاربری دیگری دارد.");
            }

            if (!string.IsNullOrWhiteSpace(password) && password.Length < 8)
            {
                return OperationResult.Fail("رمزعبور حداقل ۸ نویسه است.");
            }

            user.Username = username;
            user.EmployeeId = employeeId;
            user.RoleId = roleId;
            user.IsActive = isActive;
            if (!string.IsNullOrWhiteSpace(password))
            {
                user.PasswordHash = PasswordHasher.Hash(password);
            }

            _store.Update(user);
            await _store.SaveChangesAsync();
            return OperationResult.Ok(userId);
        }

        public async Task<OperationResult> DeleteAsync(int userId)
        {
            var user = await _store.FindAsync(userId);
            if (user is null)
            {
                return OperationResult.Fail("کاربر مورد نظر یافت نشد.");
            }

            _store.Remove(user);
            await _store.SaveChangesAsync();
            return OperationResult.Ok(userId);
        }
    }
}
