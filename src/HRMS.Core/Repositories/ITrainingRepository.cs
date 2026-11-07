using HRMS.Core.Dtos;
using HRMS.Core.Entities;

namespace HRMS.Core.Repositories
{
    public interface ITrainingRepository
    {
        Task<List<TrainingAssignmentRow>> SearchAsync(string? keyword, int? employeeId, int? courseId, string? status);
        Task<List<TrainingCourseRow>> GetCoursesAsync(bool includeInactive);
        Task<TrainingAssignment?> FindOpenAssignmentAsync(int employeeId, int courseId);
    }
}
