using System.Security.Cryptography;
using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Documents;

public sealed record DocumentRow(int Id, int EmployeeId, string Title, string? Category,
    string FileName, long FileSize, DateTimeOffset UploadedAtUtc, DateOnly? ExpiryDate);

public sealed record DocumentDownload(int EmployeeId, string FileName, byte[] Bytes, byte[] Sha256);
public sealed record DocumentPage(IReadOnlyList<DocumentRow> Rows, int Total, int Page, int PageSize);

public sealed class DocumentService(DocumentDbContext db, IEmployeeDirectory employees, TimeProvider clock)
{
    public async Task<DocumentPage> ListAsync(ActorScope actor, int employeeId, int page, int pageSize, CancellationToken ct)
    {
        RequireRead(actor);
        await RequireEmployeeAsync(actor, employeeId, ct);
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);
        var query = db.Documents.AsNoTracking()
            .Where(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId);
        var total = await query.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (int)Math.Ceiling((double)total / pageSize)));
        var rows = await query.OrderByDescending(x => x.UploadedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new DocumentRow(x.Id, x.EmployeeId, x.Title, x.Category,
                x.FileName, x.FileSize, x.UploadedAtUtc, x.ExpiryDate))
            .ToListAsync(ct);
        return new DocumentPage(rows, total, page, pageSize);
    }

    public async Task<int> UploadAsync(ActorScope actor, int employeeId, string title, string? category,
        DateOnly? expiryDate, string fileName, Stream content, CancellationToken ct)
    {
        RequireWrite(actor);
        await RequireEmployeeAsync(actor, employeeId, ct);
        var problem = DocumentRules.Validate(title, category, fileName, 1);
        if (problem is not null) throw new ArgumentException(problem);
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > DocumentRules.MaxFileSizeBytes)
                throw new ArgumentException("حجم فایل نباید بیش از ۱۰ مگابایت باشد.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        var bytes = buffer.ToArray();
        problem = DocumentRules.Validate(title, category, fileName, bytes.Length);
        if (problem is not null) throw new ArgumentException(problem);
        var hash = SHA256.HashData(bytes);
        var row = new EmployeeDocument
        {
            CompanyId = actor.CompanyId, EmployeeId = employeeId,
            Title = title.Trim(), Category = category?.Trim(), FileName = fileName,
            FileSize = bytes.Length, Bytes = bytes, Sha256 = hash,
            UploadedByUserId = actor.Id, UploadedAtUtc = clock.GetUtcNow(), ExpiryDate = expiryDate
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Documents.Add(row);
        await db.SaveChangesAsync(ct);
        db.Audits.Add(new DocumentAudit
        {
            DocumentId = row.Id, CompanyId = actor.CompanyId, EmployeeId = employeeId,
            ActorUserId = actor.Id, Action = "Upload", AtUtc = clock.GetUtcNow(), Sha256 = hash
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return row.Id;
    }

    public async Task<DocumentDownload?> DownloadAsync(ActorScope actor, int documentId, CancellationToken ct)
    {
        RequireRead(actor);
        var row = await db.Documents.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == documentId && x.CompanyId == actor.CompanyId, ct);
        if (row is null) return null;
        if (await employees.GetAsync(actor.CompanyId, row.EmployeeId, ct) is null) return null;
        if (!SHA256.HashData(row.Bytes).SequenceEqual(row.Sha256))
            throw new InvalidDataException("Document content checksum differs from its stored hash.");
        return new DocumentDownload(row.EmployeeId, DocumentRules.SafeDownloadName(row.FileName), row.Bytes, row.Sha256);
    }

    public async Task DeleteAsync(ActorScope actor, int documentId, int employeeId, CancellationToken ct)
    {
        RequireWrite(actor);
        var row = await db.Documents.SingleOrDefaultAsync(x => x.Id == documentId && x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId, ct)
            ?? throw new UnauthorizedAccessException("Document is outside the active company or missing.");
        await RequireEmployeeAsync(actor, row.EmployeeId, ct);
        db.Audits.Add(new DocumentAudit
        {
            DocumentId = row.Id, CompanyId = row.CompanyId, EmployeeId = row.EmployeeId,
            ActorUserId = actor.Id, Action = "Delete", AtUtc = clock.GetUtcNow(), Sha256 = row.Sha256
        });
        db.Documents.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    private async Task RequireEmployeeAsync(ActorScope actor, int employeeId, CancellationToken ct)
    {
        if (await employees.GetAsync(actor.CompanyId, employeeId, ct) is null)
            throw new UnauthorizedAccessException("Employee is outside the active company or missing.");
    }

    private static void RequireRead(ActorScope actor)
    {
        if (!AccessRules.CanRead(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
    }

    private static void RequireWrite(ActorScope actor)
    {
        if (!AccessRules.CanWrite(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
    }
}
