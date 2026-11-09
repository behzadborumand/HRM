using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Leave;

public sealed class LeaveDbContext(DbContextOptions<LeaveDbContext> options) : DbContext(options)
{
    public DbSet<LeavePolicy> Policies => Set<LeavePolicy>();
    public DbSet<LeaveBalance> Balances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> Requests => Set<LeaveRequest>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<LeavePolicy>(entity =>
        {
            entity.ToTable("Policies", "leave");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.CompanyId });
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(50);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.AnnualHours).HasColumnType("decimal(10,2)");
            entity.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();
        });
        model.Entity<LeaveBalance>(entity =>
        {
            entity.ToTable("Balances", "leave");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AccruedHours).HasColumnType("decimal(10,2)");
            entity.Property(x => x.UsedHours).HasColumnType("decimal(10,2)");
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.PolicyId, x.Year }).IsUnique();
            entity.HasOne<LeavePolicy>().WithMany().HasForeignKey(x => new { x.PolicyId, x.CompanyId })
                .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<LeaveRequest>(entity =>
        {
            entity.ToTable("Requests", "leave");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StartDate).HasColumnType("date");
            entity.Property(x => x.EndDate).HasColumnType("date");
            entity.Property(x => x.Hours).HasColumnType("decimal(10,2)");
            entity.Property(x => x.Reason).HasMaxLength(1000);
            entity.Property(x => x.Comments).HasMaxLength(1000);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.Status, x.StartDate });
            entity.HasOne<LeavePolicy>().WithMany().HasForeignKey(x => new { x.PolicyId, x.CompanyId })
                .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public sealed class LeavePolicy
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public string? Description { get; set; }
    public decimal AnnualHours { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class LeaveBalance
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public int PolicyId { get; set; }
    public int Year { get; set; }
    public decimal AccruedHours { get; set; }
    public decimal UsedHours { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class LeaveRequest
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public int PolicyId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal Hours { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";
    public int? RequestedByUserId { get; set; }
    public int? RequestedByEmployeeId { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public int? DecidedByUserId { get; set; }
    public int? DecidedByEmployeeId { get; set; }
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public string? Comments { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
