using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Organization;

public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Department> Departments => Set<Department>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Company>(entity =>
        {
            entity.ToTable("Companies", "organization");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });
        model.Entity<Department>(entity =>
        {
            entity.ToTable("Departments", "organization");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
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
}
