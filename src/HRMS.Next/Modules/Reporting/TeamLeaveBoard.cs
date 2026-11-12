using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Leave;
using Microsoft.EntityFrameworkCore;
namespace HRMS.Next.Modules.Reporting;
public sealed record TeamLeaveRow(int Id,int EmployeeId,string EmployeeName,string PolicyName,DateOnly StartDate,DateOnly EndDate,decimal Hours,byte[] RowVersion);
public sealed record TeamLeavePage(IReadOnlyList<TeamLeaveRow> Rows,int Total,int Page,int PageSize);
public sealed class TeamLeaveBoard(LeaveDbContext db)
{
    public async Task<TeamLeavePage> LoadAsync(ActorScope actor,int page,CancellationToken ct)
    {
        if (!AccessRules.CanUseTeam(actor)) throw new UnauthorizedAccessException();
        var managerId=actor.EmployeeId!.Value;
        var query=db.Database.SqlQuery<TeamLeaveRow>($"""
            SELECT r.Id,r.EmployeeId,e.FirstName+N' '+e.LastName AS EmployeeName,p.Name AS PolicyName,
                   r.StartDate,r.EndDate,r.Hours,r.RowVersion
            FROM [leave].[Requests] r
            JOIN [leave].[Policies] p ON p.Id=r.PolicyId AND p.CompanyId=r.CompanyId
            JOIN [personnel].[Employees] e ON e.Id=r.EmployeeId AND e.CompanyId=r.CompanyId
            JOIN [personnel].[EmployeeJobs] j ON j.EmployeeId=e.Id AND j.CompanyId=e.CompanyId AND j.IsCurrent=1
            JOIN [personnel].[Employees] m ON m.Id=j.ManagerEmployeeId AND m.CompanyId=e.CompanyId
            WHERE r.CompanyId={actor.CompanyId} AND j.ManagerEmployeeId={managerId}
              AND e.Id<>{managerId} AND e.TerminationDate IS NULL AND m.TerminationDate IS NULL AND r.Status=N'Pending'
            """);
        var total=await query.CountAsync(ct);
        page=Math.Clamp(page,1,Math.Max(1,(total+19)/20));
        var rows=await query.OrderBy(r=>r.StartDate).ThenBy(r=>r.Id).Skip((page-1)*20).Take(20).ToListAsync(ct);
        return new(rows,total,page,20);
    }
}