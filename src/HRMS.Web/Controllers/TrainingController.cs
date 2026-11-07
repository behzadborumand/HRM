using HRMS.Core.Dtos;
using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Core.Services;
using HRMS.Web.Helpers;
using HRMS.Web.Security;
using HRMS.Web.Services;
using HRMS.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS.Web.Controllers
{
    [Authorize]
    public class TrainingController : Controller
    {
        private readonly TrainingService _service;
        private readonly ITrainingRepository _training;
        private readonly IRepository<Employee> _employeeStore;

        public TrainingController(TrainingService service, ITrainingRepository training, IRepository<Employee> employeeStore)
        {
            _service = service;
            _training = training;
            _employeeStore = employeeStore;
        }

        public async Task<IActionResult> Index(int? employeeId, int? courseId, string? status, string? keyword)
        {
            var canReadAll = User.HasPermission(Permissions.EmployeesRead);
            var self = User.EmployeeId();
            if (!canReadAll && self is null)
            {
                return Forbid();
            }

            if (employeeId is null && !canReadAll)
            {
                // A plain employee has no company-wide view, so the page becomes their own training list.
                employeeId = self;
            }

            if (employeeId is not null && employeeId != self && !canReadAll)
            {
                return Forbid();
            }

            var rows = await _training.SearchAsync(keyword, employeeId, courseId, status);
            var assignedTo = employeeId is null ? null : await _employeeStore.FindAsync(employeeId.Value);

            ViewData["Title"] = employeeId is not null && employeeId == self && !canReadAll ? "آموزش‌های من" : "آموزش‌ها";

            return View(new TrainingIndexViewModel
            {
                Keyword = keyword,
                CourseId = courseId,
                Status = status,
                EmployeeId = employeeId,
                EmployeeName = assignedTo is null ? null : $"{assignedTo.FirstName} {assignedTo.LastName}",
                Rows = rows,
                Courses = await _training.GetCoursesAsync(includeInactive: false),
                Employees = canReadAll ? await EmployeeLookupsAsync() : new List<EmployeeLookup>(),
                CanWrite = User.HasPermission(Permissions.EmployeesWrite),
                Form = new TrainingAssignViewModel { EmployeeId = employeeId ?? self ?? 0 }
            });
        }

        [Authorize(Policy = Policies.EmployeesWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(TrainingAssignViewModel form)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Index), EmployeeScope(form.EmployeeId));
            }

            var result = await _service.AssignAsync(form.EmployeeId, form.CourseId, form.AssignedDate,
                form.DueDate, form.CompletedDate, form.CertificateNumber, form.Notes, User.EmployeeId());

            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? "آموزش برای کارمند ثبت شد."
                : string.Join(" ", result.Errors);

            return RedirectToAction(nameof(Index), new { employeeId = form.EmployeeId });
        }

        [Authorize(Policy = Policies.EmployeesWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int assignmentId, int employeeId, DateTime completedDate, string? certificateNumber)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Index), EmployeeScope(employeeId));
            }

            var result = await _service.CompleteAsync(assignmentId, completedDate, certificateNumber);

            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? "انجام آموزش ثبت شد."
                : string.Join(" ", result.Errors);

            return RedirectToAction(nameof(Index), EmployeeScope(employeeId));
        }

        [Authorize(Policy = Policies.EmployeesWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int assignmentId, int employeeId)
        {
            var result = await _service.DeleteAsync(assignmentId);

            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? "ثبت آموزش حذف شد."
                : string.Join(" ", result.Errors);

            return RedirectToAction(nameof(Index), EmployeeScope(employeeId));
        }

        [Authorize(Policy = Policies.OrgRead)]
        public async Task<IActionResult> Courses(int? edit = null)
        {
            ViewData["Title"] = "کاتالوگ دوره‌های آموزشی";

            var course = edit is null
                ? null
                : (await _training.GetCoursesAsync(true)).FirstOrDefault(c => c.CourseId == edit);

            return View(new TrainingCoursesViewModel
            {
                Courses = await _training.GetCoursesAsync(true),
                Form = course is null
                    ? new TrainingCourseFormViewModel { Active = true }
                    : new TrainingCourseFormViewModel
                    {
                        CourseId = course.CourseId,
                        Title = course.Title,
                        Category = course.Category,
                        Provider = course.Provider,
                        Description = course.Description,
                        RenewalDays = course.RenewalDays,
                        MandatoryForAll = course.MandatoryForAll,
                        Active = course.Active
                    }
            });
        }

        [Authorize(Policy = Policies.OrgWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveCourse(TrainingCourseFormViewModel form)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Courses), form.CourseId is null ? null : new { edit = form.CourseId });
            }

            var result = await _service.SaveCourseAsync(form.CourseId, form.Title, form.Category, form.Provider,
                form.Description, form.RenewalDays, form.MandatoryForAll, form.Active);

            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? (form.CourseId is null ? "دوره آموزشی تازه ساخته شد." : "دوره آموزشی به‌روزرسانی شد.")
                : string.Join(" ", result.Errors);

            return RedirectToAction(nameof(Courses), result.Succeeded && form.CourseId is not null ? new { edit = form.CourseId } : null);
        }

        [Authorize(Policy = Policies.OrgWrite)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCourse(int courseId)
        {
            var result = await _service.DeleteCourseAsync(courseId);

            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? "دوره آموزشی حذف شد."
                : string.Join(" ", result.Errors);

            return RedirectToAction(nameof(Courses));
        }

        private static object? EmployeeScope(int employeeId) => employeeId > 0 ? new { employeeId } : null;

        private async Task<List<EmployeeLookup>> EmployeeLookupsAsync()
        {
            var employees = await _employeeStore.ListAsync();

            return employees
                .Select(e => new EmployeeLookup { EmployeeId = e.EmployeeId, FullName = $"{e.FirstName} {e.LastName}" })
                .OrderBy(e => e.FullName)
                .ToList();
        }
    }
}
