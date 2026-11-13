using HRMS.Next.Modules.Access;
using Microsoft.EntityFrameworkCore;
namespace HRMS.Next.Modules.Personnel;

public sealed class EmployeeEmploymentStatusChange
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "";
    public int? ActorUserId { get; set; }
    public DateTimeOffset AtUtc { get; set; }
}
public sealed record EmploymentStatusRow(long Id, DateOnly? EffectiveDate, string? FromStatus, string ToStatus);
public sealed class EmployeeEmploymentService(PersonnelDbContext db)
{
    public async Task<IReadOnlyList<EmploymentStatusRow>> HistoryAsync(ActorScope actor,int employeeId,CancellationToken ct)
    {
        await PersonnelReadScope.RequireAsync(db,actor,employeeId,ct);
        return await db.EmploymentStatusChanges.AsNoTracking().Where(x=>x.CompanyId==actor.CompanyId && x.EmployeeId==employeeId)
            .OrderByDescending(x=>x.EffectiveDate).ThenByDescending(x=>x.Id).Take(100)
            .Select(x=>new EmploymentStatusRow(x.Id,x.EffectiveDate,x.FromStatus,x.ToStatus)).ToListAsync(ct);
    }
}