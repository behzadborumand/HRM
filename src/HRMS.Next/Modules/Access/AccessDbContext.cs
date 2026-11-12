using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Access;

public sealed class AccessDbContext(DbContextOptions<AccessDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AccessAudit> Audits => Set<AccessAudit>();
    public DbSet<AccessRole> Roles => Set<AccessRole>();
    public DbSet<RoleAudit> RoleAudits => Set<RoleAudit>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AccessRole>(role => {
            role.ToTable("Roles", "access");
            role.HasKey(x => x.Id);
            role.HasAlternateKey(x => new { x.Id, x.CompanyId });
            role.Property(x => x.Name).HasMaxLength(100).IsRequired();
            role.Property(x => x.Description).HasMaxLength(500);
            role.Property(x => x.RowVersion).IsRowVersion();
            role.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();
        });
        model.Entity<RoleAudit>(audit => {
            audit.ToTable("RoleAudits", "access");
            audit.HasKey(x => x.Id);
            audit.Property(x => x.Action).HasMaxLength(30).IsRequired();
            audit.Property(x => x.Summary).HasMaxLength(1000).IsRequired();
            audit.HasIndex(x => new { x.CompanyId, x.RoleId, x.AtUtc });
            audit.HasOne<AccessRole>().WithMany().HasForeignKey(x => new { x.RoleId, x.CompanyId })
                .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        });
        var user = model.Entity<AppUser>();
        user.HasOne<AccessRole>().WithMany().HasForeignKey(x => new { x.RoleId, x.CompanyId })
            .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        user.ToTable("Users", "access");
        user.HasKey(x => x.Id);
        user.Property(x => x.Username).HasMaxLength(100).IsRequired();
        user.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        user.HasIndex(x => x.Username).IsUnique();
        user.Property(x => x.RowVersion).IsRowVersion();
        user.HasIndex(x => new { x.CompanyId, x.EmployeeId }).IsUnique().HasFilter("[EmployeeId] IS NOT NULL");
        model.Entity<AccessAudit>(audit => {
            audit.ToTable("Audits", "access");
            audit.HasKey(x => x.Id);
            audit.Property(x => x.Action).HasMaxLength(30).IsRequired();
            audit.HasIndex(x => new { x.CompanyId, x.TargetUserId, x.AtUtc });
        });
    }
}

public sealed class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int CompanyId { get; set; }
    public int? EmployeeId { get; set; }
    public int? RoleId { get; set; }
    public bool IsAdmin { get; set; }
    public bool CanReadEmployees { get; set; }
    public bool CanWriteEmployees { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
public sealed class AccessAudit
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int TargetUserId { get; set; }
    public int ActorUserId { get; set; }
    public DateTimeOffset AtUtc { get; set; }
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
}
