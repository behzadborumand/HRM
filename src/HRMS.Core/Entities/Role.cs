namespace HRMS.Core.Entities
{
    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Permissions { get; set; }
    }
}
