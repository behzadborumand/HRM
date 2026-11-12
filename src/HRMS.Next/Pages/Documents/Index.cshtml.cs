using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Documents;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Pages.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.Next.Pages.Documents;

[Authorize]
[RequestSizeLimit(12 * 1024 * 1024)]
public sealed class IndexModel(AccessService access, EmployeeService employees, DocumentService documents) : PageModel
{
    [BindProperty] public string Title { get; set; } = "";
    [BindProperty] public string? Category { get; set; }
    [BindProperty] public string? ExpiryDateJalali { get; set; }
    [BindProperty] public IFormFile? UploadFile { get; set; }
    [BindProperty] public int DocumentId { get; set; }

    public Employee? Employee { get; private set; }
    public DocumentPage Result { get; private set; } = new([], 0, 1, 20);
    public bool CanWrite { get; private set; }

    public async Task<IActionResult> OnGetAsync(int employeeId, int page, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanReadEmployee(actor, actor.CompanyId, employeeId)) return Forbid();
        return await LoadAsync(actor, employeeId, ct, page) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostUploadAsync(int employeeId, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        if (UploadFile is null)
            return await ErrorAsync(actor, employeeId, "فایل را انتخاب کنید.", ct);
        DateOnly? expiry = null;
        if (!string.IsNullOrWhiteSpace(ExpiryDateJalali))
        {
            if (!JalaliDate.TryParse(ExpiryDateJalali, out var parsed))
                return await ErrorAsync(actor, employeeId, "تاریخ انقضای شمسی معتبر نیست.", ct);
            expiry = parsed;
        }
        try
        {
            await using var stream = UploadFile.OpenReadStream();
            await documents.UploadAsync(actor, employeeId, Title, Category, expiry,
                UploadFile.FileName, stream, ct);
        }
        catch (ArgumentException error) { return await ErrorAsync(actor, employeeId, error.Message, ct); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        return RedirectToPage(new { employeeId });
    }

    public async Task<IActionResult> OnGetDownloadAsync(int employeeId, int id, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanReadEmployee(actor, actor.CompanyId, employeeId)) return Forbid();
        var file = await documents.DownloadAsync(actor, id, ct);
        if (file is null || file.EmployeeId != employeeId) return NotFound();
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Bytes, "application/octet-stream", file.FileName);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int employeeId, CancellationToken ct)
    {
        var actor = await access.GetActiveAsync(User, ct);
        if (actor is null || !AccessRules.CanWrite(actor, actor.CompanyId)) return Forbid();
        try
        {
            await documents.DeleteAsync(actor, DocumentId, employeeId, ct);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        return RedirectToPage(new { employeeId });
    }

    private async Task<bool> LoadAsync(ActorScope actor, int employeeId, CancellationToken ct, int page = 1)
    {
        Employee = await employees.GetAsync(actor, employeeId, ct);
        if (Employee is null) return false;
        Result = await documents.ListAsync(actor, employeeId, page, 20, ct);
        CanWrite = AccessRules.CanWrite(actor, actor.CompanyId);
        return true;
    }

    private async Task<IActionResult> ErrorAsync(ActorScope actor, int employeeId, string message, CancellationToken ct)
    {
        ModelState.AddModelError("", message);
        return await LoadAsync(actor, employeeId, ct) ? Page() : NotFound();
    }
}
