using HRMS.Core.Entities;
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
    public class EmployeesController : Controller
    {
        private const int PageSize = 15;

        private readonly EmployeeService _service;
        private readonly IEmployeeRepository _employees;
        private readonly ITimeOffRepository _timeOff;
        private readonly DocumentService _documents;
        private readonly EmployeePhotoService _photos;
        private readonly ITrainingRepository _trainings;
        private readonly IRepository<Department> _departments;
        private readonly IRepository<JobTitle> _jobTitles;
        private readonly IRepository<Company> _companies;

        public EmployeesController(
            EmployeeService service,
            IEmployeeRepository employees,
            ITimeOffRepository timeOff,
            DocumentService documents,
            EmployeePhotoService photos,
            ITrainingRepository trainings,
            IRepository<Department> departments,
            IRepository<JobTitle> jobTitles,
            IRepository<Company> companies)
        {
            _service = service;
            _employees = employees;
            _timeOff = timeOff;
            _documents = documents;
            _photos = photos;
            _trainings = trainings;
            _departments = departments;
            _jobTitles = jobTitles;
            _companies = companies;
        }

        [Authorize(Policy = Policies.EmployeesRead)]
        public async Task<IActionResult> Index(string? keyword, int? departmentId, string? status, int page = 1)
        {
            ViewData["Title"] = "لیست کارکنان";

            var model = new EmployeeIndexViewModel
            {
                Keyword = keyword,
                DepartmentId = departmentId,
                Status = status,
                Page = page,
                PageSize = PageSize,
                Departments = await _departments.ListAsync()
            };

            model.Result = await _employees.SearchAsync(keyword, departmentId, status, page, PageSize);
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var profile = await _employees.GetProfileAsync(id);
            if (profile is null)
            {
                return NotFound();
            }

            if (User.EmployeeId() != id && !User.HasPermission(Permissions.EmployeesRead))
            {
                return Forbid();
            }

            ViewData["Title"] = profile.FullName;

            return View(new EmployeeDetailsViewModel
            {
                Profile = profile,
                Balances = await _timeOff.GetBalancesAsync(id, DateTime.Now.Year),
                Requests = (await _timeOff.SearchAsync(id, null, 1, 20)).Items.ToList(),
                JobHistory = await _employees.GetJobHistoryAsync(id),
                DirectReports = await _employees.GetDirectReportsAsync(id),
                Documents = await _documents.ListAsync(id),
                Trainings = await _trainings.SearchAsync(null, id, null, null),
                Neighbours = await _employees.GetNeighboursAsync(id)
            });
        }

        // The URL carries the employee's ModifiedDate, so a replaced photo is a new URL and this can be cached.
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> Photo(int id)
        {
            if (User.EmployeeId() != id && !User.HasPermission(Permissions.EmployeesRead))
            {
                return Forbid();
            }

            var photo = await _photos.OpenAsync(id);
            if (photo is null)
            {
                return NotFound();
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return PhysicalFile(photo.Value.FullPath, photo.Value.ContentType);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(PhotoRules.MaxPhotoBytes * 2)]
        public async Task<IActionResult> UploadPhoto(int employeeId, IFormFile? file)
        {
            if (!CanManagePhoto(employeeId))
            {
                return Forbid();
            }

            if (file is null)
            {
                TempData["Error"] = "فایل انتخاب نشده است.";
                return RedirectToAction(nameof(Details), new { id = employeeId });
            }

            var result = await _photos.SetAsync(employeeId, file);
            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? "تصویر کارمند ثبت شد."
                : string.Join(" ", result.Errors);

            return RedirectToAction(nameof(Details), new { id = employeeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePhoto(int employeeId)
        {
            if (!CanManagePhoto(employeeId))
            {
                return Forbid();
            }

            var result = await _photos.ClearAsync(employeeId);
            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? "تصویر کارمند حذف شد."
                : string.Join(" ", result.Errors);

            return RedirectToAction(nameof(Details), new { id = employeeId });
        }

        // Anyone may keep their own picture; HR keeps the same right over every profile.
        private bool CanManagePhoto(int employeeId) =>
            User.HasPermission(Permissions.EmployeesWrite) || User.EmployeeId() == employeeId;

        [Authorize(Policy = Policies.EmployeesWrite)]
        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "افزودن کارکنان جدید";
            var model = new EmployeeFormViewModel();
            await PopulateLookupsAsync(model);
            return View("Form", model);
        }

        [Authorize(Policy = Policies.EmployeesWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeFormViewModel model)
        {
            ViewData["Title"] = "افزودن کارکنان جدید";

            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync(model);
                return View("Form", model);
            }

            var result = await _service.CreateAsync(model);
            if (!result.Succeeded)
            {
                AddErrors(result.Errors);
                await PopulateLookupsAsync(model);
                return View("Form", model);
            }

            TempData["Message"] = $"کارمند {model.FirstName} {model.LastName} با موفقیت ثبت شد.";
            return RedirectToAction(nameof(Details), new { id = result.EntityId });
        }

        [Authorize(Policy = Policies.EmployeesWrite)]
        public async Task<IActionResult> Edit(int id)
        {
            var profile = await _employees.GetProfileAsync(id);
            if (profile is null)
            {
                return NotFound();
            }

            ViewData["Title"] = $"ویرایش {profile.FullName}";
            var model = ToFormModel(profile);
            await PopulateLookupsAsync(model);
            return View("Form", model);
        }

        [Authorize(Policy = Policies.EmployeesWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EmployeeFormViewModel model)
        {
            ViewData["Title"] = "ویرایش اطلاعات کارکنان";

            if (model.EmployeeId != id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync(model);
                return View("Form", model);
            }

            var result = await _service.UpdateAsync(id, model);
            if (!result.Succeeded)
            {
                AddErrors(result.Errors);
                await PopulateLookupsAsync(model);
                return View("Form", model);
            }

            TempData["Message"] = "اطلاعات کارمند با موفقیت به‌روزرسانی شد.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Policy = Policies.EmployeesWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Terminate(int id, DateTime terminationDate)
        {
            var result = await _service.TerminateAsync(id, terminationDate);
            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? "خروج کارمند ثبت و شغل او بسته شد."
                : string.Join(" ", result.Errors);
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Policy = Policies.EmployeesWrite)]
        public async Task<IActionResult> BulkEdit(List<int> ids)
        {
            if (ids.Count == 0)
            {
                TempData["Error"] = "برای ویرایش گروهی دست‌کم یک کارمند را انتخاب کنید.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["Title"] = "ویرایش گروهی کارکنان";

            var model = new BulkEditViewModel { EmployeeIds = ids };
            await PopulateBulkLookupsAsync(model);
            return View(model);
        }

        [Authorize(Policy = Policies.EmployeesWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkApply(BulkEditViewModel model)
        {
            var result = await _service.BulkUpdateAsync(model);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" ", result.Errors);
                // Repeated keys, because a comma-joined list is not bound back to List<int> by the query binder.
                var ids = string.Join("&", (model.EmployeeIds ?? new List<int>()).Select(id => $"ids={id}"));
                return ids.Length == 0
                    ? RedirectToAction(nameof(Index))
                    : LocalRedirect($"/Employees/BulkEdit?{ids}");
            }

            TempData["Message"] = result.EntityId == 0
                ? "هیچ تغییری لازم نبود؛ مقادیر انتخابی با وضعیت فعلی یکی بود."
                : $"{PersianDate.ToPersianDigits(result.EntityId.ToString())} کارمند به‌روزرسانی شد و در سوابق شغلی ثبت گردید.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = Policies.EmployeesDelete)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)        {
            var result = await _service.DeleteAsync(id);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" ", result.Errors);
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Message"] = "رکورد کارمند حذف شد.";
            return RedirectToAction(nameof(Index));
        }

        private void AddErrors(IEnumerable<string> errors)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        private static EmployeeFormViewModel ToFormModel(HRMS.Core.Dtos.EmployeeProfile profile) => new()
        {
            EmployeeId = profile.EmployeeId,
            EmployeeNumber = profile.EmployeeNumber,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            PreferredName = profile.PreferredName,
            Email = profile.Email,
            Phone = profile.Phone,
            DateOfBirth = profile.DateOfBirth,
            Gender = profile.Gender,
            MaritalStatus = profile.MaritalStatus,
            HireDate = profile.HireDate,
            EmploymentStatus = profile.EmploymentStatus,
            TerminationDate = profile.TerminationDate,
            DepartmentId = profile.DepartmentId,
            JobTitleId = profile.JobTitleId,
            ManagerId = profile.ManagerId,
            EmploymentType = profile.EmploymentType,
            PayType = profile.PayType,
            PayRate = profile.PayRate,
            AddressLine1 = profile.AddressLine1,
            City = profile.City,
            State = profile.State,
            PostalCode = profile.PostalCode,
            Country = profile.Country,
            HomePhone = profile.HomePhone,
            MobilePhone = profile.MobilePhone,
            EmergencyContactName = profile.EmergencyContactName,
            EmergencyContactPhone = profile.EmergencyContactPhone,
            EmergencyContactRelation = profile.EmergencyContactRelation
        };

        private async Task PopulateBulkLookupsAsync(BulkEditViewModel model)
        {
            model.Employees = await _employees.GetByIdsAsync(model.EmployeeIds);
            model.Departments = await _departments.ListAsync();
            model.JobTitles = (await _jobTitles.ListAsync()).OrderBy(t => t.TitleName).ToList();
            model.Managers = await _employees.GetManagerCandidatesAsync();
        }

        private async Task PopulateLookupsAsync(EmployeeFormViewModel model)
        {
            model.Departments = await _departments.ListAsync();
            model.JobTitles = (await _jobTitles.ListAsync()).OrderBy(t => t.TitleName).ToList();
            model.Companies = await _companies.ListAsync();
            model.Managers = await _employees.GetManagerCandidatesAsync(model.IsNew ? null : model.EmployeeId);

            if (model.CompanyId is null)
            {
                model.CompanyId = await _companies.Query().Select(c => (int?)c.CompanyId).FirstOrDefaultAsync();
            }
        }
    }
}
