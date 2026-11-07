namespace HRMS.Core.Entities
{
    public class User
    {
        public int UserId { get; set; }
        public int EmployeeId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastLogin { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
