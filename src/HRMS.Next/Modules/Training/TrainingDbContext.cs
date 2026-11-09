using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Training;

public sealed class TrainingDbContext(DbContextOptions<TrainingDbContext> options) : DbContext(options)
{
    public DbSet<TrainingCourse> Courses => Set<TrainingCourse>();
    public DbSet<TrainingAssignment> Assignments => Set<TrainingAssignment>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<TrainingCourse>(entity =>
        {
            entity.ToTable("Courses", "training");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.CompanyId });
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(50);
            entity.Property(x => x.Provider).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => new { x.CompanyId, x.Title }).IsUnique();
        });
        model.Entity<TrainingAssignment>(entity =>
        {
            entity.ToTable("Assignments", "training");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CertificateNumber).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.DueDate });
            entity.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.CourseId }).IsUnique()
                .HasFilter("[CompletedDate] IS NULL");
            entity.HasOne<TrainingCourse>().WithMany().HasForeignKey(x => new { x.CourseId, x.CompanyId })
                .HasPrincipalKey(x => new { x.Id, x.CompanyId }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public sealed class TrainingCourse
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Title { get; set; } = "";
    public string? Category { get; set; }
    public string? Provider { get; set; }
    public string? Description { get; set; }
    public int? RenewalDays { get; set; }
    public bool MandatoryForAll { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class TrainingAssignment
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public int CourseId { get; set; }
    public DateOnly AssignedDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public string? CertificateNumber { get; set; }
    public string? Notes { get; set; }
    public int AssignedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
