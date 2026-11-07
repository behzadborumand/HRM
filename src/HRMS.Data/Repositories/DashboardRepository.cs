using HRMS.Core.Dtos;
using HRMS.Core.Entities;
using HRMS.Core.Repositories;
using HRMS.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Data.Repositories;

public class DashboardRepository(HrmsDbContext db) : IDashboardRepository
{
    public async Task<List<DashboardPerson>> GetCurrentPeopleAsync(DateTime today) =>
        await db.Employees.AsNoTracking()
            .Where(e => (e.EmploymentStatus == "Active" || e.EmploymentStatus == "OnLeave")
                && e.HireDate < today.Date.AddDays(1)
                && (e.TerminationDate == null || e.TerminationDate > today.Date))
            .Select(e => new DashboardPerson(e.EmployeeId, e.FirstName + " " + e.LastName,
                e.DateOfBirth, e.HireDate,
                db.EmployeeJobs.Where(j => j.EmployeeId == e.EmployeeId && j.IsCurrent)
                    .Select(j => db.JobTitles.Where(t => t.JobTitleId == j.JobTitleId).Select(t => t.TitleName).FirstOrDefault()).FirstOrDefault(),
                e.AvatarPath != null && e.AvatarPath != ""))
            .ToListAsync();

    public async Task<List<OrganizationLink>> GetActiveLinksAsync() =>
        await db.OrganizationLinks.AsNoTracking().Where(l => l.Active)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Title).ThenBy(l => l.LinkId).ToListAsync();
}
