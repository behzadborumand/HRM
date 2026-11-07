namespace HRMS.Core.Dtos
{
    public class AuthenticatedUser
    {
        public int UserId { get; init; }
        public int EmployeeId { get; init; }
        public string Username { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string RoleName { get; init; } = string.Empty;
        public string? Permissions { get; init; }
        public string PasswordHash { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }

    public class UserListItem
    {
        public int UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string RoleName { get; init; } = string.Empty;
        public int RoleId { get; init; }
        public int EmployeeId { get; init; }
        public bool IsActive { get; init; }
        public DateTime? LastLogin { get; init; }
        public DateTime CreatedDate { get; init; }
    }
}
