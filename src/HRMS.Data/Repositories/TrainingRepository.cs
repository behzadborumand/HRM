using HRMS.Core.Dtos;
using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Core.Services;
using HRMS.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Data.Repositories
{
    public class TrainingRepository : ITrainingRepository
    {
        private readonly HrmsDbContext _db;

        public TrainingRepository(HrmsDbContext db)
        {
            _db = db;
        }

        public async Task<List<TrainingAssignmentRow>> SearchAsync(string? keyword, int? employeeId, int? courseId, string? status)
        {
            var today = DateTime.Today;
            var query = AssignmentRows();

            if (employeeId.HasValue)
            {
                query = query.Where(a => a.EmployeeId == employeeId);
            }

            if (courseId.HasValue)
            {
                query = query.Where(a => a.CourseId == courseId);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(a => a.EmployeeName.Contains(keyword) || a.CourseTitle.Contains(keyword));
            }

            query = status switch
            {
                TrainingRules.StatusCompleted => query.Where(a => a.CompletedDate != null),
                TrainingRules.StatusOverdue => query.Where(a => a.CompletedDate == null && a.DueDate != null && a.DueDate < today),
                TrainingRules.StatusPending => query.Where(a => a.CompletedDate == null && (a.DueDate == null || a.DueDate >= today)),
                _ => query
            };

            return await query
                // بازها بالا می‌مانند و بین آن‌ها سررسیدِ گذشته از سررسیدِ بی‌تاریخ زودتر می‌آید.
                .OrderBy(a => a.CompletedDate != null)
                .ThenBy(a => a.DueDate == null)
                .ThenBy(a => a.DueDate)
                .ThenByDescending(a => a.AssignmentId)
                .ToListAsync();
        }

        public async Task<List<TrainingCourseRow>> GetCoursesAsync(bool includeInactive)
        {
            var query = _db.TrainingCourses.AsNoTracking();
            if (!includeInactive)
            {
                query = query.Where(c => c.Active);
            }

            return await query
                .OrderBy(c => c.Title)
                .Select(c => new TrainingCourseRow
                {
                    CourseId = c.CourseId,
                    Title = c.Title,
                    Category = c.Category,
                    Provider = c.Provider,
                    Description = c.Description,
                    RenewalDays = c.RenewalDays,
                    MandatoryForAll = c.MandatoryForAll,
                    Active = c.Active,
                    CreatedDate = c.CreatedDate,
                    AssignedCount = _db.TrainingAssignments.Count(a => a.CourseId == c.CourseId),
                    OpenCount = _db.TrainingAssignments.Count(a => a.CourseId == c.CourseId && a.CompletedDate == null)
                })
                .ToListAsync();
        }

        public async Task<TrainingAssignment?> FindOpenAssignmentAsync(int employeeId, int courseId) =>
            await _db.TrainingAssignments.FirstOrDefaultAsync(a =>
                a.EmployeeId == employeeId && a.CourseId == courseId && a.CompletedDate == null);

        private IQueryable<TrainingAssignmentRow> AssignmentRows()
        {
            return from assignment in _db.TrainingAssignments.AsNoTracking()
                   join employee in _db.Employees on assignment.EmployeeId equals employee.EmployeeId
                   join course in _db.TrainingCourses on assignment.CourseId equals course.CourseId
                   join assigner in _db.Employees on assignment.AssignedBy equals (int?)assigner.EmployeeId into assignerGroup
                   from assigner in assignerGroup.DefaultIfEmpty()
                   select new TrainingAssignmentRow
                   {
                       AssignmentId = assignment.AssignmentId,
                       EmployeeId = employee.EmployeeId,
                       EmployeeName = employee.FirstName + " " + employee.LastName,
                       DepartmentName = _db.EmployeeJobs
                           .Where(j => j.EmployeeId == assignment.EmployeeId && j.IsCurrent)
                           .Select(j => _db.Departments.Where(d => d.DepartmentId == j.DepartmentId).Select(d => d.DepartmentName).FirstOrDefault())
                           .FirstOrDefault(),
                       CourseId = course.CourseId,
                       CourseTitle = course.Title,
                       CourseCategory = course.Category,
                       RenewalDays = course.RenewalDays,
                       AssignedDate = assignment.AssignedDate,
                       DueDate = assignment.DueDate,
                       CompletedDate = assignment.CompletedDate,
                       CertificateNumber = assignment.CertificateNumber,
                       Notes = assignment.Notes,
                       AssignedByName = assigner == null ? null : assigner.FirstName + " " + assigner.LastName
                   };
        }
    }
}
