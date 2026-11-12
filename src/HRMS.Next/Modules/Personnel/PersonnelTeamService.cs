using HRMS.Next.Modules.Access;
using Microsoft.EntityFrameworkCore;
namespace HRMS.Next.Modules.Personnel;
public sealed record TeamPerson(int Id,string EmployeeNumber,string FullName,string? JobTitle,int DepartmentId);
public sealed record TeamPage(IReadOnlyList<TeamPerson> Rows,int Total,int Page,int PageSize);
public sealed class PersonnelTeamService(PersonnelDbContext db)
{
    public async Task<TeamPage> ListAsync(ActorScope actor,string? query,int page,CancellationToken ct)
    {
        if (!AccessRules.CanUseTeam(actor)) throw new UnauthorizedAccessException();
        var people = PersonnelReadScope.DirectReports(db, actor.CompanyId, actor.EmployeeId!.Value);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term=query.Trim();
            people=people.Where(e=>e.FirstName.Contains(term)||e.LastName.Contains(term)||e.EmployeeNumber.Contains(term));
        }
        var total=await people.CountAsync(ct);
        page=Math.Clamp(page,1,Math.Max(1,(total+19)/20));
        var rows=await people.OrderBy(e=>e.LastName).ThenBy(e=>e.FirstName).ThenBy(e=>e.Id).Skip((page-1)*20).Take(20)
            .Select(e=>new TeamPerson(e.Id,e.EmployeeNumber,e.FirstName+" "+e.LastName,
                db.EmployeeJobs.Where(j=>j.CompanyId==actor.CompanyId&&j.EmployeeId==e.Id&&j.IsCurrent).Select(j=>j.JobTitle).FirstOrDefault(),e.DepartmentId)).ToListAsync(ct);
        return new(rows,total,page,20);
    }
}