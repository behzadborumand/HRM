using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Core.Services;
using HRMS.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Web.Services
{
    /// <summary>
    /// Writes training records. Status is never stored, so every mutation only touches dates and the
    /// rules in <see cref="TrainingRules"/> decide whether the row reads as pending, overdue or completed.
    /// </summary>
    public class TrainingService
    {
        private readonly ITrainingRepository _training;
        private readonly IRepository<Employee> _employees;
        private readonly IRepository<TrainingCourse> _courses;
        private readonly IRepository<TrainingAssignment> _assignments;

        public TrainingService(ITrainingRepository training, IRepository<Employee> employees,
            IRepository<TrainingCourse> courses, IRepository<TrainingAssignment> assignments)
        {
            _training = training;
            _employees = employees;
            _courses = courses;
            _assignments = assignments;
        }

        public async Task<OperationResult> AssignAsync(int employeeId, int courseId, DateTime assignedDate,
            DateTime? dueDate, DateTime? completedDate, string? certificateNumber, string? notes, int? assignedBy)
        {
            var course = await _courses.FindAsync(courseId);
            if (course is null || !course.Active)
            {
                return OperationResult.Fail("دوره انتخاب‌شده دیگر فعال نیست.");
            }

            if (await _employees.FindAsync(employeeId) is null)
            {
                return OperationResult.Fail("کارمند انتخاب‌شده یافت نشد.");
            }

            var problem = TrainingRules.ValidateAssignment(assignedDate, dueDate, completedDate, DateTime.Today);
            if (problem is not null)
            {
                return OperationResult.Fail(problem);
            }

            if (await _training.FindOpenAssignmentAsync(employeeId, courseId) is not null)
            {
                return OperationResult.Fail($"«{course.Title}» برای این کارمند هنوز تکمیل نشده است؛ همان ثبت را به‌روز کنید.");
            }

            var assignment = new TrainingAssignment
            {
                EmployeeId = employeeId,
                CourseId = courseId,
                AssignedDate = assignedDate.Date,
                DueDate = (dueDate ?? TrainingRules.DefaultDueDate(assignedDate, course.RenewalDays))?.Date,
                CompletedDate = completedDate?.Date,
                CertificateNumber = TrimToNull(certificateNumber, 100),
                Notes = TrimToNull(notes, 500),
                AssignedBy = assignedBy
            };

            await _assignments.AddAsync(assignment);
            await _assignments.SaveChangesAsync();

            return OperationResult.Ok(assignment.AssignmentId);
        }

        public async Task<OperationResult> CompleteAsync(int assignmentId, DateTime completedDate, string? certificateNumber)
        {
            var assignment = await _assignments.FindAsync(assignmentId);
            if (assignment is null)
            {
                return OperationResult.Fail("ثبت آموزش موردنظر یافت نشد.");
            }

            var problem = TrainingRules.ValidateAssignment(assignment.AssignedDate, assignment.DueDate, completedDate, DateTime.Today);
            if (problem is not null)
            {
                return OperationResult.Fail(problem);
            }

            assignment.CompletedDate = completedDate.Date;
            var certificate = TrimToNull(certificateNumber, 100);
            if (certificate is not null)
            {
                assignment.CertificateNumber = certificate;
            }

            _assignments.Update(assignment);
            await _assignments.SaveChangesAsync();

            return OperationResult.Ok(assignmentId);
        }

        public async Task<OperationResult> DeleteAsync(int assignmentId)
        {
            var assignment = await _assignments.FindAsync(assignmentId);
            if (assignment is null)
            {
                return OperationResult.Fail("ثبت آموزش موردنظر یافت نشد.");
            }

            _assignments.Remove(assignment);
            await _assignments.SaveChangesAsync();

            return OperationResult.Ok(assignmentId);
        }

        public async Task<OperationResult> SaveCourseAsync(int? courseId, string? title, string? category, string? provider,
            string? description, int? renewalDays, bool mandatoryForAll, bool active)
        {
            var problem = TrainingRules.ValidateCourse(title, renewalDays);
            if (problem is not null)
            {
                return OperationResult.Fail(problem);
            }

            var course = courseId is null ? new TrainingCourse() : await _courses.FindAsync(courseId.Value);
            if (courseId is not null && course is null)
            {
                return OperationResult.Fail("دوره انتخاب‌شده یافت نشد.");
            }

            course!.Title = title!.Trim();
            course.Category = TrimToNull(category, 50);
            course.Provider = TrimToNull(provider, 200);
            course.Description = TrimToNull(description, 1000);
            course.RenewalDays = renewalDays;
            course.MandatoryForAll = mandatoryForAll;
            course.Active = active;

            if (courseId is null)
            {
                course.CreatedDate = DateTime.Now;
                await _courses.AddAsync(course);
            }
            else
            {
                _courses.Update(course);
            }

            await _courses.SaveChangesAsync();

            return OperationResult.Ok(course.CourseId);
        }

        /// <summary>Courses with training records are deactivated instead of deleted so history keeps its meaning.</summary>
        public async Task<OperationResult> DeleteCourseAsync(int courseId)
        {
            var course = await _courses.FindAsync(courseId);
            if (course is null)
            {
                return OperationResult.Fail("دوره انتخاب‌شده یافت نشد.");
            }

            var assignedCount = await _assignments.Query().CountAsync(a => a.CourseId == courseId);
            if (assignedCount > 0)
            {
                return OperationResult.Fail($"این دوره در {PersianDate.ToPersianDigits(assignedCount.ToString())} ثبت آموزشی استفاده شده است؛ آن را از فهرست خارج کنید.");
            }

            _courses.Remove(course);
            await _courses.SaveChangesAsync();

            return OperationResult.Ok(courseId);
        }

        private static string? TrimToNull(string? value, int maxLength)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
        }
    }
}
