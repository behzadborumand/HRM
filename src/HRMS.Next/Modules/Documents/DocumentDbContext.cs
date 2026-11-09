using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Documents;

public sealed class DocumentDbContext(DbContextOptions<DocumentDbContext> options) : DbContext(options)
{
    public DbSet<EmployeeDocument> Documents => Set<EmployeeDocument>();
    public DbSet<DocumentAudit> Audits => Set<DocumentAudit>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<EmployeeDocument>(entity =>
        {
            entity.ToTable("EmployeeDocuments", "documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(50);
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.Bytes).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(32).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.UploadedAtUtc });
        });
        model.Entity<DocumentAudit>(entity =>
        {
            entity.ToTable("DocumentAudits", "documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.EmployeeId, x.AtUtc });
        });
    }
}

public sealed class EmployeeDocument
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public string Title { get; set; } = "";
    public string? Category { get; set; }
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public byte[] Bytes { get; set; } = [];
    public byte[] Sha256 { get; set; } = [];
    public int UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class DocumentAudit
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public int ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public DateTimeOffset AtUtc { get; set; }
    public byte[] Sha256 { get; set; } = [];
}
