using HRMS.Core.Repositories;
using HRMS.Core.Services;
using HRMS.Web.Helpers;
using HRMS.Web.Security;
using HRMS.Web.Services;
using HRMS.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Web.Controllers
{
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly DocumentService _service;
        private readonly IEmployeeRepository _employees;
        private readonly IRepository<HRMS.Core.Entities.EmployeeDocument> _documents;

        public DocumentsController(DocumentService service, IEmployeeRepository employees,
            IRepository<HRMS.Core.Entities.EmployeeDocument> documents)
        {
            _service = service;
            _employees = employees;
            _documents = documents;
        }

        public async Task<IActionResult> Index(int employeeId)
        {
            var profile = await _employees.GetProfileAsync(employeeId);
            if (profile is null)
            {
                return NotFound();
            }

            if (!CanRead(employeeId))
            {
                return Forbid();
            }

            ViewData["Title"] = $"مدارک {profile.FullName}";

            return View(new EmployeeDocumentsViewModel
            {
                EmployeeId = employeeId,
                EmployeeName = profile.FullName,
                EmployeeNumber = profile.EmployeeNumber ?? "-",
                Documents = await _service.ListAsync(employeeId),
                AllowedExtensions = DocumentRules.ExtensionList(),
                MaxSizeLabel = PersianDate.ToPersianDigits($"{DocumentRules.MaxFileSizeBytes / (1024 * 1024)} مگابایت"),
                Upload = new DocumentUploadViewModel { EmployeeId = employeeId }
            });
        }

        [Authorize(Policy = Policies.EmployeesWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(DocumentRules.MaxFileSizeBytes * 2)]
        public async Task<IActionResult> Upload(DocumentUploadViewModel model)
        {
            if (model.File is null)
            {
                ModelState.AddModelError(nameof(model.File), "فایل انتخاب نشده است.");
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Index), new { employeeId = model.EmployeeId });
            }

            var result = await _service.AddAsync(model.EmployeeId, model.File!, model.Title, model.Category,
                model.ExpiryDate, User.UserId());

            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? "سند در پرونده کارمند ثبت شد."
                : string.Join(" ", result.Errors);

            return RedirectToAction(nameof(Index), new { employeeId = model.EmployeeId });
        }

        public async Task<IActionResult> Download(int id)
        {
            var employeeId = await _documents.Query()
                .Where(d => d.DocumentId == id)
                .Select(d => (int?)d.EmployeeId)
                .FirstOrDefaultAsync();

            if (employeeId is null)
            {
                return NotFound();
            }

            if (!CanRead(employeeId.Value))
            {
                return Forbid();
            }

            var file = await _service.OpenAsync(id);
            if (file is null)
            {
                return NotFound();
            }

            return PhysicalFile(file.Value.FullPath, file.Value.Document.ContentType ?? "application/octet-stream",
                file.Value.Document.FileName);
        }

        [Authorize(Policy = Policies.EmployeesWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int employeeId)
        {
            var result = await _service.DeleteAsync(id);
            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? "سند و فایل آن حذف شد."
                : string.Join(" ", result.Errors);

            return RedirectToAction(nameof(Index), new { employeeId });
        }

        private bool CanRead(int employeeId) =>
            User.EmployeeId() == employeeId || User.HasPermission(Permissions.EmployeesRead);
    }
}
