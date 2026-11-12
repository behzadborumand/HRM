using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Pages.Employees;

[Authorize]
[RequestSizeLimit(3 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 3 * 1024 * 1024)]
public sealed class PhotoModel(AccessService access, EmployeePhotoService photos) : PageModel
{
    [BindProperty] public IFormFile? UploadFile { get; set; }
    [BindProperty] public string? Version { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanReadEmployee(actor, actor.CompanyId, id)) return Forbid();
        var photo = await photos.ReadAsync(actor, id, ct);
        if (photo is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(photo.Bytes, photo.ContentType);
    }

    public async Task<IActionResult> OnPostUploadAsync(int id, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        try
        {
            if (UploadFile is null || UploadFile.Length is < 20 or > EmployeePhotoService.MaxBytes)
                throw new ArgumentException("یک تصویر PNG یا JPEG تا ۲ مگابایت انتخاب کنید.");
            using var buffer = new MemoryStream();
            await UploadFile.CopyToAsync(buffer, ct);
            await photos.SaveAsync(actor, id, buffer.ToArray(), UploadFile.ContentType,
                string.IsNullOrEmpty(Version) ? null : Convert.FromBase64String(Version), ct);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception error) when (error is ArgumentException or FormatException or DbUpdateException)
        {
            TempData["PhotoError"] = error is DbUpdateException or FormatException
                ? "تصویر پرونده تغییر کرده است. صفحه را بازخوانی و دوباره تلاش کنید." : error.Message;
        }
        return RedirectToPage("/Employees/Details", new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        try { await photos.DeleteAsync(actor, id, Convert.FromBase64String(Version ?? ""), ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception error) when (error is FormatException or DbUpdateConcurrencyException)
        {
            TempData["PhotoError"] = "تصویر پرونده تغییر کرده است. صفحه را بازخوانی کنید.";
        }
        return RedirectToPage("/Employees/Details", new { id });
    }
}
