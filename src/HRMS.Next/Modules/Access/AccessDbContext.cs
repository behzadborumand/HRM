using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Access;

public sealed class AccessDbContext(DbContextOptions<AccessDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var user = model.Entity<AppUser>();
        user.ToTable("Users", "access");
        user.HasKey(x => x.Id);
        user.Property(x => x.Username).HasMaxLength(100).IsRequired();
        user.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        user.HasIndex(x => x.Username).IsUnique();
    }
}

public sealed class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int CompanyId { get; set; }
    public bool IsAdmin { get; set; }
    public bool CanReadEmployees { get; set; }
    public bool CanWriteEmployees { get; set; }
    public bool IsActive { get; set; } = true;
}
