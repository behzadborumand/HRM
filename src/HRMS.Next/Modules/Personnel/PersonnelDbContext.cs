using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Personnel;

public sealed class PersonnelDbContext(DbContextOptions<PersonnelDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeAudit> Audits => Set<EmployeeAudit>();

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
