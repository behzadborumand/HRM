using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Web.Services
{
    /// <summary>
    /// Keeps employee files under App_Data/uploads (never served as static content) and stores only
    /// the generated relative path in the database, so a renamed original cannot steer the filesystem.
    /// </summary>
    public class DocumentService
    {
        private readonly IRepository<EmployeeDocument> _documents;
        private readonly IEmployeeRepository _employees;
        private readonly string _rootPath;

        public DocumentService(IRepository<EmployeeDocument> documents, IEmployeeRepository employees, IWebHostEnvironment env)
        {
            _documents = documents;
            _employees = employees;
            _rootPath = Path.Combine(env.ContentRootPath, "App_Data", "uploads");
        }

        public async Task<List<EmployeeDocument>> ListAsync(int employeeId) =>
            await _documents.Query().Where(d => d.EmployeeId == employeeId)
                .OrderByDescending(d => d.UploadDate).ToListAsync();

        public async Task<OperationResult> AddAsync(int employeeId, IFormFile file, string? title, string? category,
            DateTime? expiryDate, int? userId)
        {
            var problem = DocumentRules.Validate(file.FileName, file.Length);
            if (problem is not null)
            {
                return OperationResult.Fail(problem);
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return OperationResult.Fail("عنوان سند الزامی است.");
            }

            if (await _employees.GetProfileAsync(employeeId) is null)
            {
                return OperationResult.Fail("کارمند انتخاب‌شده یافت نشد.");
            }

            var directory = Path.Combine(_rootPath, employeeId.ToString());
            Directory.CreateDirectory(directory);

            var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
            await using (var stream = File.Create(Path.Combine(directory, storedName)))
            {
                await file.CopyToAsync(stream);
            }

            var document = new EmployeeDocument
            {
                EmployeeId = employeeId,
                Title = title.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
                FileName = Path.GetFileName(file.FileName),
                StoredPath = Path.Combine(employeeId.ToString(), storedName),
                ContentType = file.ContentType,
                FileSize = file.Length,
                UploadedBy = userId,
                UploadDate = DateTime.Now,
                ExpiryDate = expiryDate
            };

            await _documents.AddAsync(document);
            await _documents.SaveChangesAsync();

            return OperationResult.Ok(document.DocumentId);
        }

        public async Task<(EmployeeDocument Document, string FullPath)?> OpenAsync(int documentId)
        {
            var document = await _documents.FindAsync(documentId);
            if (document is null)
            {
                return null;
            }

            var fullPath = Path.GetFullPath(Path.Combine(_rootPath, document.StoredPath));
            if (!fullPath.StartsWith(Path.GetFullPath(_rootPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || !File.Exists(fullPath))
            {
                return null;
            }

            return (document, fullPath);
        }

        public async Task<OperationResult> DeleteAsync(int documentId)
        {
            var document = await _documents.FindAsync(documentId);
            if (document is null)
            {
                return OperationResult.Fail("سند موردنظر یافت نشد.");
            }

            _documents.Remove(document);
            await _documents.SaveChangesAsync();

            var fullPath = Path.GetFullPath(Path.Combine(_rootPath, document.StoredPath));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            return OperationResult.Ok();
        }
    }
}
