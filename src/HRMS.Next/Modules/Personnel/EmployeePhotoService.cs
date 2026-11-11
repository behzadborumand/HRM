using System.Security.Cryptography;
using HRMS.Next.Modules.Access;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Personnel;

public sealed class EmployeePhoto
{
    public int EmployeeId { get; set; }
    public int CompanyId { get; set; }
    public byte[] Bytes { get; set; } = [];
    public string ContentType { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTimeOffset UploadedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed record PhotoSummary(string Sha256, byte[] RowVersion);
public sealed record PhotoDownload(byte[] Bytes, string ContentType);

public sealed class EmployeePhotoService(PersonnelDbContext db, TimeProvider clock)
{
    public const int MaxBytes = 2 * 1024 * 1024;

    public async Task<PhotoSummary?> SummaryAsync(ActorScope actor, int employeeId, CancellationToken ct)
    {
        RequireRead(actor);
        return await db.Photos.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.CompanyId == actor.CompanyId)
            .Select(x => new PhotoSummary(x.Sha256, x.RowVersion)).SingleOrDefaultAsync(ct);
    }

    public async Task<PhotoDownload?> ReadAsync(ActorScope actor, int employeeId, CancellationToken ct)
    {
        RequireRead(actor);
        var photo = await db.Photos.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.CompanyId == actor.CompanyId, ct);
        if (photo is null) return null;
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(photo.Bytes)), photo.Sha256, StringComparison.Ordinal))
            throw new InvalidDataException("Photo checksum does not match.");
        return new PhotoDownload(photo.Bytes, photo.ContentType);
    }

    public async Task SaveAsync(ActorScope actor, int employeeId, byte[] bytes, string contentType,
        byte[]? expectedVersion, CancellationToken ct)
    {
        await RequireWriteAsync(actor, employeeId, ct);
        ValidateImage(bytes, contentType);
        var photo = await db.Photos.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.CompanyId == actor.CompanyId, ct);
        if ((photo is null && expectedVersion is not null) ||
            (photo is not null && (expectedVersion is null || !photo.RowVersion.SequenceEqual(expectedVersion))))
            throw new DbUpdateConcurrencyException("تصویر پرونده تغییر کرده است. صفحه را بازخوانی کنید.");
        if (photo is null)
        {
            photo = new EmployeePhoto { CompanyId = actor.CompanyId, EmployeeId = employeeId };
            db.Photos.Add(photo);
        }
        photo.Bytes = bytes.ToArray();
        photo.ContentType = contentType;
        photo.Sha256 = Convert.ToHexString(SHA256.HashData(bytes));
        photo.UploadedAtUtc = clock.GetUtcNow();
        Audit(actor, employeeId, "PhotoUpload");
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ActorScope actor, int employeeId, byte[] expectedVersion, CancellationToken ct)
    {
        await RequireWriteAsync(actor, employeeId, ct);
        var photo = await db.Photos.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.CompanyId == actor.CompanyId, ct);
        if (photo is null || !photo.RowVersion.SequenceEqual(expectedVersion))
            throw new DbUpdateConcurrencyException("تصویر پرونده تغییر کرده است. صفحه را بازخوانی کنید.");
        db.Photos.Remove(photo);
        Audit(actor, employeeId, "PhotoDelete");
        await db.SaveChangesAsync(ct);
    }

    private static void ValidateImage(byte[] bytes, string contentType)
    {
        if (bytes.Length is < 20 or > MaxBytes)
            throw new ArgumentException("تصویر باید حداکثر ۲ مگابایت باشد.");
        ReadOnlySpan<byte> pngHeader = [137, 80, 78, 71, 13, 10, 26, 10];
        ReadOnlySpan<byte> pngEnd = [73, 69, 78, 68, 174, 66, 96, 130];
        var png = contentType == "image/png" && bytes.AsSpan().StartsWith(pngHeader) && bytes.AsSpan().EndsWith(pngEnd);
        var jpeg = contentType == "image/jpeg" && bytes[0] == 255 && bytes[1] == 216 &&
            bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217;
        if (!png && !jpeg) throw new ArgumentException("فقط تصویر PNG یا JPEG معتبر انتخاب کنید.");
    }

    private async Task RequireWriteAsync(ActorScope actor, int employeeId, CancellationToken ct)
    {
        if (!AccessRules.CanWrite(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
        if (!await db.Employees.AsNoTracking().AnyAsync(x => x.Id == employeeId && x.CompanyId == actor.CompanyId, ct))
            throw new KeyNotFoundException("پروندهٔ کارمند یافت نشد.");
    }

    private static void RequireRead(ActorScope actor)
    {
        if (!AccessRules.CanRead(actor, actor.CompanyId)) throw new UnauthorizedAccessException();
    }

    private void Audit(ActorScope actor, int employeeId, string action) =>
        db.Audits.Add(new EmployeeAudit { EmployeeId = employeeId, ActorUserId = actor.Id,
            AtUtc = clock.GetUtcNow(), Action = action, Summary = "Employee portrait" });
}
