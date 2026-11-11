using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using Microsoft.EntityFrameworkCore;
namespace HRMS.Next.Modules.Training;

public sealed record ProfileTrainingAssignment(int Id, string CourseTitle, string? Provider, DateOnly AssignedDate, DateOnly? DueDate, DateOnly? CompletedDate, string Status);
public sealed record EmployeeTrainingProfile(IReadOnlyList<ProfileTrainingAssignment> Assignments, int Total, int Page, int PageSize);
public interface IEmployeeTrainingProfile
{
    Task<EmployeeTrainingProfile?> ReadAsync(ActorScope actor, int employeeId, int page, CancellationToken ct);
}
public sealed class TrainingProfileService(TrainingDbContext db, IEmployeeDirectory employees, TimeProvider clock) : IEmployeeTrainingProfile
{
    public async Task<EmployeeTrainingProfile?> ReadAsync(ActorScope actor, int employeeId, int page, CancellationToken ct)
    {
        if (!AccessRules.CanReadEmployeeActivity(actor, actor.CompanyId, employeeId)) throw new UnauthorizedAccessException();
        if (await employees.GetAsync(actor.CompanyId, employeeId, ct) is null) return null;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);
        var history = from assignment in db.Assignments.AsNoTracking()
            join course in db.Courses.AsNoTracking() on new { Id = assignment.CourseId, assignment.CompanyId } equals new { course.Id, course.CompanyId }
            where assignment.CompanyId == actor.CompanyId && assignment.EmployeeId == employeeId
            select new { assignment.Id, course.Title, course.Provider, assignment.AssignedDate, assignment.DueDate, assignment.CompletedDate };
        var total = await history.CountAsync(ct);
        const int pageSize = 20;
        page = Math.Clamp(page, 1, Math.Max(1, (total - 1) / pageSize + 1));
        var rows = await history.OrderByDescending(x => x.AssignedDate).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(x => new ProfileTrainingAssignment(x.Id, x.Title, x.Provider, x.AssignedDate, x.DueDate, x.CompletedDate,
            TrainingRules.ResolveStatus(x.CompletedDate, x.DueDate, today))).ToList(), total, page, pageSize);
    }
}
