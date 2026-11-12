namespace HRMS.Next.Modules.Access;

public sealed class AccessRole
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool Active { get; set; } = true;
    public bool CanReadEmployees { get; set; }
    public bool CanWriteEmployees { get; set; }
    public bool CanManageTeam { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class RoleAudit
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int RoleId { get; set; }
    public int ActorUserId { get; set; }
    public DateTimeOffset AtUtc { get; set; }
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
}
