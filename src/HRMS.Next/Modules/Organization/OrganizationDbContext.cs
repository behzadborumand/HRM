using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Organization;

public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<JobTitle> JobTitles => Set<JobTitle>();
    public DbSet<OrganizationLink> OrganizationLinks => Set<OrganizationLink>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Company>(entity =>
        {
            entity.ToTable("Companies", "organization");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
        model.Entity<OrganizationLink>(entity =>
        {
            entity.ToTable("OrganizationLinks", "organization", table =>
                table.HasCheckConstraint("CK_OrganizationLinks_SortOrder", "[SortOrder] >= 0 AND [SortOrder] <= 9999"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.CompanyId, x.Active, x.SortOrder });
            entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<JobTitle>(entity =>
        {
            entity.ToTable("JobTitles", "organization");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();
            entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Department>(entity =>
        {
            entity.ToTable("Departments", "organization");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();
            entity.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Department>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public sealed class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Department
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = "";
    public int? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}

public sealed class JobTitle
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}

public sealed class OrganizationLink
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
}
