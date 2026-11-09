using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Personnel;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next.Modules.Training;

public sealed record TrainingAssignmentRow(TrainingAssignment Assignment, string Status);

public sealed class TrainingService(TrainingDbContext db, IEmployeeDirectory employees, TimeProvider clock)
{
    public async Task<int> CreateCourseAsync(ActorScope actor, string title, string? category, string? provider,
        string? description, int? renewalDays, bool mandatoryForAll, CancellationToken ct)
    {
        RequireAdmin(actor);
        title = TrainingRules.ValidateCourse(title, renewalDays);
        if (category?.Length > 50 || provider?.Length > 200 || description?.Length > 1000)
            throw new ArgumentException("Course details exceed their size limits.");
        if (await db.Courses.AnyAsync(x => x.CompanyId == actor.CompanyId && x.Title == title, ct))
            throw new ArgumentException("Course title already exists in this company.");
        var course = new TrainingCourse
        {
            CompanyId = actor.CompanyId, Title = title, Category = category?.Trim(), Provider = provider?.Trim(),
            Description = description?.Trim(), RenewalDays = renewalDays, MandatoryForAll = mandatoryForAll,
            CreatedAtUtc = clock.GetUtcNow()
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync(ct);
        return course.Id;
    }

    public async Task UpdateCourseAsync(ActorScope actor, int courseId, string title, string? category,
        string? provider, string? description, int? renewalDays, bool mandatoryForAll, CancellationToken ct)
    {
        RequireAdmin(actor);
        title = TrainingRules.ValidateCourse(title, renewalDays);
        if (category?.Length > 50 || provider?.Length > 200 || description?.Length > 1000)
            throw new ArgumentException("Course details exceed their size limits.");
        var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == courseId && x.CompanyId == actor.CompanyId, ct)
            ?? throw new UnauthorizedAccessException("Course is outside the active company.");
        if (await db.Courses.AnyAsync(x => x.CompanyId == actor.CompanyId && x.Id != courseId && x.Title == title, ct))
            throw new ArgumentException("Course title already exists in this company.");
        course.Title = title;
        course.Category = category?.Trim();
        course.Provider = provider?.Trim();
        course.Description = description?.Trim();
        course.RenewalDays = renewalDays;
        course.MandatoryForAll = mandatoryForAll;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetCourseActiveAsync(ActorScope actor, int courseId, bool active, CancellationToken ct)
    {
        RequireAdmin(actor);
        var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == courseId && x.CompanyId == actor.CompanyId, ct)
            ?? throw new UnauthorizedAccessException("Course is outside the active company.");
        course.Active = active;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> AssignAsync(ActorScope actor, int employeeId, int courseId, DateOnly assignedDate,
        DateOnly? dueDate, CancellationToken ct)
    {
        RequireAdmin(actor);
        var employee = await employees.GetAsync(actor.CompanyId, employeeId, ct)
            ?? throw new UnauthorizedAccessException("Employee is outside the active company.");
        if (assignedDate < employee.HireDate || assignedDate > employee.TerminationDate)
            throw new ArgumentException("Assignment is outside employment dates.");
        TrainingRules.ValidateAssignment(assignedDate, dueDate, null, TodayInIran());
        var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == courseId && x.CompanyId == actor.CompanyId && x.Active, ct)
            ?? throw new ArgumentException("Active course was not found.");
        if (await db.Assignments.AnyAsync(x => x.CompanyId == actor.CompanyId && x.EmployeeId == employeeId &&
            x.CourseId == courseId && x.CompletedDate == null, ct))
            throw new ArgumentException("An open assignment already exists for this employee and course.");
        var assignment = new TrainingAssignment
        {
            CompanyId = actor.CompanyId, EmployeeId = employeeId, CourseId = courseId,
            AssignedDate = assignedDate, DueDate = dueDate ?? TrainingRules.NextDueDate(assignedDate, course.RenewalDays),
            AssignedByUserId = actor.Id
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync(ct);
        return assignment.Id;
    }

    public async Task CompleteAsync(ActorScope actor, int assignmentId, DateOnly completedDate,
        string? certificateNumber, CancellationToken ct)
    {
        RequireAdmin(actor);
        var assignment = await db.Assignments.SingleOrDefaultAsync(x => x.Id == assignmentId && x.CompanyId == actor.CompanyId, ct)
            ?? throw new UnauthorizedAccessException("Assignment is outside the active company.");
        if (assignment.CompletedDate is not null) throw new ArgumentException("Assignment is already completed.");
        TrainingRules.ValidateAssignment(assignment.AssignedDate, assignment.DueDate, completedDate,
            TodayInIran());
        if (certificateNumber?.Length > 100) throw new ArgumentException("Certificate number is too long.");
        assignment.CompletedDate = completedDate;
        assignment.CertificateNumber = certificateNumber?.Trim();
        await db.SaveChangesAsync(ct);
    }

    public Task<List<TrainingCourse>> CoursesAsync(ActorScope actor, CancellationToken ct)
    {
        RequireAdmin(actor);
        return db.Courses.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId)
            .OrderBy(x => x.Title).Take(100).ToListAsync(ct);
    }

    public async Task<List<TrainingAssignmentRow>> AssignmentsAsync(ActorScope actor, CancellationToken ct)
    {
        RequireAdmin(actor);
        var rows = await db.Assignments.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId)
            .OrderByDescending(x => x.AssignedDate).ThenByDescending(x => x.Id).Take(100).ToListAsync(ct);
        var today = TodayInIran();
        return rows.Select(x => new TrainingAssignmentRow(x,
            TrainingRules.ResolveStatus(x.CompletedDate, x.DueDate, today))).ToList();
    }

    private DateOnly TodayInIran() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time")).DateTime);

    private static void RequireAdmin(ActorScope actor)
    {
        if (!actor.IsActive || !actor.IsAdmin || actor.CompanyId <= 0)
            throw new UnauthorizedAccessException();
    }
}
