using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Web.Services
{
    /// <summary>
    /// Keeps one photo per employee under App_Data/photos (never served as static content); the database
    /// holds only the generated relative name, so the browser-supplied filename never reaches the disk.
    /// </summary>
    public class EmployeePhotoService
    {
        private readonly IRepository<Employee> _employees;
        private readonly string _rootPath;

        public EmployeePhotoService(IRepository<Employee> employees, IWebHostEnvironment env)
        {
            _employees = employees;
            _rootPath = Path.Combine(env.ContentRootPath, "App_Data", "photos");
        }

        public async Task<OperationResult> SetAsync(int employeeId, IFormFile file)
        {
            var header = new byte[PhotoRules.HeaderBytes];
            await using (var input = file.OpenReadStream())
            {
                var read = await input.ReadAsync(header);
                header = header.AsSpan(0, read).ToArray();
            }

            var (extension, problem) = PhotoRules.Inspect(file.FileName, file.Length, header);
            if (problem is not null)
            {
                return OperationResult.Fail(problem);
            }

            var employee = await _employees.FindAsync(employeeId);
            if (employee is null)
            {
                return OperationResult.Fail("کارمند انتخاب‌شده یافت نشد.");
            }

            var storedName = $"{employeeId}{extension!}";
            var temporaryPath = Path.Combine(_rootPath, $"{storedName}.{Guid.NewGuid():N}.tmp");

            Directory.CreateDirectory(_rootPath);
            await using (var output = File.Create(temporaryPath))
            {
                await file.CopyToAsync(output);
            }

            ReplaceOnDisk(employee.AvatarPath, temporaryPath, storedName);

            employee.AvatarPath = storedName;
            employee.ModifiedDate = DateTime.Now;
            _employees.Update(employee);
            await _employees.SaveChangesAsync();

            return OperationResult.Ok();
        }

        public async Task<OperationResult> ClearAsync(int employeeId)
        {
            var employee = await _employees.FindAsync(employeeId);
            if (employee is null)
            {
                return OperationResult.Fail("کارمند انتخاب‌شده یافت نشد.");
            }

            DeleteFile(employee.AvatarPath);

            employee.AvatarPath = null;
            employee.ModifiedDate = DateTime.Now;
            _employees.Update(employee);
            await _employees.SaveChangesAsync();

            return OperationResult.Ok();
        }

        /// <summary>Path and content type of the stored image, or null when there is nothing to serve.</summary>
        public async Task<(string FullPath, string ContentType)?> OpenAsync(int employeeId)
        {
            var avatarPath = await _employees.Query()
                .Where(e => e.EmployeeId == employeeId)
                .Select(e => e.AvatarPath)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(avatarPath))
            {
                return null;
            }

            var contentType = PhotoRules.ContentTypeFor(avatarPath);
            var fullPath = Path.GetFullPath(Path.Combine(_rootPath, avatarPath));

            if (contentType is null
                || !fullPath.StartsWith(Path.GetFullPath(_rootPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || !File.Exists(fullPath))
            {
                return null;
            }

            return (fullPath, contentType);
        }

        /// <summary>Drops the image file of an employee that is being deleted; the row itself goes next.</summary>
        public void DeleteFile(Employee employee) => DeleteFile(employee.AvatarPath);

        private void ReplaceOnDisk(string? previousPath, string temporaryPath, string storedName)
        {
            var finalPath = Path.Combine(_rootPath, storedName);
            File.Move(temporaryPath, finalPath, overwrite: true);

            if (!string.IsNullOrWhiteSpace(previousPath) &&
                !string.Equals(previousPath, storedName, StringComparison.OrdinalIgnoreCase))
            {
                DeleteFile(previousPath);
            }
        }

        private void DeleteFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
            if (fullPath.StartsWith(Path.GetFullPath(_rootPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}
