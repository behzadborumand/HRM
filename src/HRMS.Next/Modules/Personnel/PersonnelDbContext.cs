using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Personnel;

public sealed class PersonnelDbContext(DbContextOptions<PersonnelDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeePhoto> Photos => Set<EmployeePhoto>();
    public DbSet<EmployeeAudit> Audits => Set<EmployeeAudit>();
    public DbSet<EmployeeJob> EmployeeJobs => Set<EmployeeJob>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees", "personnel");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EmployeeNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.CompanyId, x.EmployeeNumber }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.DepartmentId, x.LastName });
            entity.HasIndex(x => new { x.CompanyId, x.LastName, x.FirstName, x.Id });
        });
        model.Entity<EmployeePhoto>(entity =>
        {
            entity.ToTable("EmployeePhotos", "personnel");
            entity.HasKey(x => x.EmployeeId);
            entity.Property(x => x.Bytes).HasMaxLength(EmployeePhotoService.MaxBytes).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasOne<Employee>().WithOne().HasForeignKey<EmployeePhoto>(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CompanyId);
        });
        model.Entity<EmployeeJob>(entity =>
        {
            entity.ToTable("EmployeeJobs", "personnel", table =>
                table.HasCheckConstraint("CK_EmployeeJobs_DateRange", "[EndDate] IS NULL OR [EndDate] >= [StartDate]"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.JobTitle).HasMaxLength(200);
            entity.Property(x => x.EmploymentType).HasMaxLength(50);
            entity.Property(x => x.PayType).HasMaxLength(50);
            entity.Property(x => x.PayRate).HasPrecision(18, 2);
            entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Employee>().WithMany().HasForeignKey(x => x.ManagerEmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.StartDate });
            entity.HasIndex(x => new { x.CompanyId, x.EmployeeId }).IsUnique().HasFilter("[IsCurrent] = 1");
        });
        model.Entity<EmployeeAudit>(entity =>
        {
            entity.ToTable("EmployeeAudits", "personnel");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.EmployeeId, x.AtUtc });
        });
    }
}

public sealed class Employee
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int DepartmentId { get; set; }
    public string EmployeeNumber { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public DateOnly HireDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class EmployeeAudit
{
    public long Id { get; set; }
    public int EmployeeId { get; set; }
    public int ActorUserId { get; set; }
    public DateTimeOffset AtUtc { get; set; }
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
}
