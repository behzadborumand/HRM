using HRMS.Core.Dtos;
using HRMS.Core.Entities;

namespace HRMS.Core.Repositories;

public interface IDashboardRepository
{
    Task<List<DashboardPerson>> GetCurrentPeopleAsync(DateTime today);
    Task<List<OrganizationLink>> GetActiveLinksAsync();
}
