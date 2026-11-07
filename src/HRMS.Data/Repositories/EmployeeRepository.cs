using System.Linq.Expressions;
using HRMS.Core.Dtos;
using HRMS.Core.Entities;
using HRMS.Core.Paging;
using HRMS.Core.Repositories;
using HRMS.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Data.Repositories
{
    public class EmployeeRepository : IEmployeeRepository
    {
        private readonly HrmsDbContext _db;
        private readonly Expression<Func<Employee, EmployeeListItem>> _listItemProjection;

        public EmployeeRepository(HrmsDbContext db)
        {
            _db = db;

            // Reused by both list queries; capturing the context parameter keeps the subqueries translatable.
            _listItemProjection = e => new EmployeeListItem
            {
                EmployeeId = e.EmployeeId,
                EmployeeNumber = e.EmployeeNumber,
                FirstName = e.FirstName,
                LastName = e.LastName,
                Email = e.Email,
                Phone = e.Phone,
                HireDate = e.HireDate,
                EmploymentStatus = e.EmploymentStatus,
                AvatarPath = e.AvatarPath,
                ModifiedDate = e.ModifiedDate,
                DepartmentName = db.EmployeeJobs
                    .Where(j => j.EmployeeId == e.EmployeeId && j.IsCurrent)
                    .Select(j => db.Departments.Where(d => d.DepartmentId == j.DepartmentId).Select(d => d.DepartmentName).FirstOrDefault())
                    .FirstOrDefault(),
                JobTitleName = db.EmployeeJobs
                    .Where(j => j.EmployeeId == e.EmployeeId && j.IsCurrent)
                    .Select(j => db.JobTitles.Where(t => t.JobTitleId == j.JobTitleId).Select(t => t.TitleName).FirstOrDefault())
                    .FirstOrDefault(),
                ManagerName = db.EmployeeJobs
                    .Where(j => j.EmployeeId == e.EmployeeId && j.IsCurrent)
                    .Select(j => db.Employees.Where(m => m.EmployeeId == j.ManagerId).Select(m => m.FirstName + " " + m.LastName).FirstOrDefault())
                    .FirstOrDefault(),
                PayRate = db.EmployeeJobs
                    .Where(j => j.EmployeeId == e.EmployeeId && j.IsCurrent)
                    .Select(j => j.PayRate)
                    .FirstOrDefault()
            };
        }

        public async Task<PagedResult<EmployeeListItem>> SearchAsync(string? keyword, int? departmentId, string? status, int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 200);

            var query = _db.Employees.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var term = keyword.Trim();
                query = query.Where(e =>
                    e.FirstName.Contains(term) ||
                    e.LastName.Contains(term) ||
                    (e.EmployeeNumber != null && e.EmployeeNumber.Contains(term)) ||
                    (e.Email != null && e.Email.Contains(term)) ||
                    (e.Phone != null && e.Phone.Contains(term)));
            }

            if (departmentId.HasValue)
            {
                query = query.Where(e => _db.EmployeeJobs.Any(j =>
                    j.EmployeeId == e.EmployeeId && j.IsCurrent && j.DepartmentId == departmentId));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(e => e.EmploymentStatus == status);
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(_listItemProjection)
                .ToListAsync();

            return new PagedResult<EmployeeListItem>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<List<EmployeeListItem>> GetByIdsAsync(IReadOnlyCollection<int> employeeIds)
        {
            // Ordered before projecting so EF keeps sorting on real columns instead of the computed names.
            return await _db.Employees.AsNoTracking()
                .Where(e => employeeIds.Contains(e.EmployeeId))
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .Select(_listItemProjection)
                .ToListAsync();
        }

        public async Task<EmployeeProfile?> GetProfileAsync(int employeeId)
        {
            var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
            if (employee is null)
            {
                return null;
            }

            var job = await _db.EmployeeJobs.AsNoTracking()
                .Where(j => j.EmployeeId == employeeId && j.IsCurrent)
                .Select(j => new
                {
                    j.DepartmentId,
                    j.JobTitleId,
                    j.ManagerId,
                    j.EmploymentType,
                    j.PayType,
                    j.PayRate,
                    DepartmentName = _db.Departments.Where(d => d.DepartmentId == j.DepartmentId).Select(d => d.DepartmentName).FirstOrDefault(),
                    JobTitleName = _db.JobTitles.Where(t => t.JobTitleId == j.JobTitleId).Select(t => t.TitleName).FirstOrDefault(),
                    ManagerName = _db.Employees.Where(m => m.EmployeeId == j.ManagerId).Select(m => m.FirstName + " " + m.LastName).FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            var personal = await _db.EmployeePersonalInfos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.EmployeeId == employeeId);

            var companyName = employee.CompanyId.HasValue
                ? await _db.Companies.Where(c => c.CompanyId == employee.CompanyId).Select(c => c.CompanyName).FirstOrDefaultAsync()
                : null;

            return new EmployeeProfile
            {
                EmployeeId = employee.EmployeeId,
                EmployeeNumber = employee.EmployeeNumber,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                PreferredName = employee.PreferredName,
                Email = employee.Email,
                Phone = employee.Phone,
                DateOfBirth = employee.DateOfBirth,
                Gender = employee.Gender,
                MaritalStatus = employee.MaritalStatus,
                HireDate = employee.HireDate,
                TerminationDate = employee.TerminationDate,
                EmploymentStatus = employee.EmploymentStatus,
                AvatarPath = employee.AvatarPath,
                ModifiedDate = employee.ModifiedDate,
                CompanyName = companyName,
                DepartmentId = job?.DepartmentId,
                DepartmentName = job?.DepartmentName,
                JobTitleId = job?.JobTitleId,
                JobTitleName = job?.JobTitleName,
                ManagerId = job?.ManagerId,
                ManagerName = job?.ManagerName,
                EmploymentType = job?.EmploymentType,
                PayType = job?.PayType,
                PayRate = job?.PayRate,
                AddressLine1 = personal?.AddressLine1,
                City = personal?.City,
                State = personal?.State,
                PostalCode = personal?.PostalCode,
                Country = personal?.Country,
                HomePhone = personal?.HomePhone,
                MobilePhone = personal?.MobilePhone,
                EmergencyContactName = personal?.EmergencyContactName,
                EmergencyContactPhone = personal?.EmergencyContactPhone,
                EmergencyContactRelation = personal?.EmergencyContactRelation
            };
        }

        public async Task<List<EmployeeLookup>> GetManagerCandidatesAsync(int? excludeEmployeeId = null)
        {
            return await _db.Employees.AsNoTracking()
                .Where(e => e.EmploymentStatus == "Active" && (excludeEmployeeId == null || e.EmployeeId != excludeEmployeeId))
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .Select(e => new EmployeeLookup
                {
                    EmployeeId = e.EmployeeId,
                    FullName = e.FirstName + " " + e.LastName,
                    JobTitleName = _db.EmployeeJobs
                        .Where(j => j.EmployeeId == e.EmployeeId && j.IsCurrent)
                        .Select(j => _db.JobTitles.Where(t => t.JobTitleId == j.JobTitleId).Select(t => t.TitleName).FirstOrDefault())
                        .FirstOrDefault()
                })
                .ToListAsync();
        }

        public async Task<List<EmployeeJobHistoryRow>> GetJobHistoryAsync(int employeeId)
        {
            return await _db.EmployeeJobs.AsNoTracking()
                .Where(j => j.EmployeeId == employeeId)
                .OrderByDescending(j => j.StartDate)
                .Select(j => new EmployeeJobHistoryRow
                {
                    EmployeeJobId = j.EmployeeJobId,
                    StartDate = j.StartDate,
                    EndDate = j.EndDate,
                    IsCurrent = j.IsCurrent,
                    DepartmentName = _db.Departments.Where(d => d.DepartmentId == j.DepartmentId).Select(d => d.DepartmentName).FirstOrDefault(),
                    JobTitleName = _db.JobTitles.Where(t => t.JobTitleId == j.JobTitleId).Select(t => t.TitleName).FirstOrDefault(),
                    ManagerName = _db.Employees.Where(m => m.EmployeeId == j.ManagerId).Select(m => m.FirstName + " " + m.LastName).FirstOrDefault(),
                    EmploymentType = j.EmploymentType,
                    PayType = j.PayType,
                    PayRate = j.PayRate
                })
                .ToListAsync();
        }

        public async Task<List<EmployeeLookup>> GetDirectReportsAsync(int managerEmployeeId)
        {
            return await _db.Employees.AsNoTracking()
                .Where(e => _db.EmployeeJobs.Any(j => j.EmployeeId == e.EmployeeId && j.IsCurrent && j.ManagerId == managerEmployeeId))
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .Select(e => new EmployeeLookup
                {
                    EmployeeId = e.EmployeeId,
                    FullName = e.FirstName + " " + e.LastName,
                    JobTitleName = _db.EmployeeJobs
                        .Where(j => j.EmployeeId == e.EmployeeId && j.IsCurrent)
                        .Select(j => _db.JobTitles.Where(t => t.JobTitleId == j.JobTitleId).Select(t => t.TitleName).FirstOrDefault())
                        .FirstOrDefault()
                })
                .ToListAsync();
        }

        public async Task<EmployeeNeighbours?> GetNeighboursAsync(int employeeId)
        {
            // One int column for the whole company keeps the ordering identical to the employee list.
            var orderedIds = await _db.Employees.AsNoTracking()
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ThenBy(e => e.EmployeeId)
                .Select(e => e.EmployeeId)
                .ToListAsync();

            var index = orderedIds.IndexOf(employeeId);
            if (index < 0)
            {
                return null;
            }

            return new EmployeeNeighbours
            {
                PreviousEmployeeId = index > 0 ? orderedIds[index - 1] : null,
                NextEmployeeId = index < orderedIds.Count - 1 ? orderedIds[index + 1] : null,
                Position = index + 1,
                Total = orderedIds.Count
            };
        }

        public async Task<string> NextEmployeeNumberAsync()
        {
            var numbers = await _db.Employees.AsNoTracking()
                .Where(e => e.EmployeeNumber != null)
                .Select(e => e.EmployeeNumber!)
                .ToListAsync();

            var max = 0;
            foreach (var number in numbers)
            {
                if (number.StartsWith("EMP-") && int.TryParse(number[4..], out var value))
                {
                    max = Math.Max(max, value);
                }
            }

            return $"EMP-{max + 1:0000}";
        }

        public async Task<bool> IsEmployeeNumberTakenAsync(string employeeNumber, int? excludeEmployeeId = null) =>
            await _db.Employees.AsNoTracking().AnyAsync(e =>
                e.EmployeeNumber == employeeNumber && (excludeEmployeeId == null || e.EmployeeId != excludeEmployeeId));

        public async Task<bool> IsEmailTakenAsync(string email, int? excludeEmployeeId = null) =>
            await _db.Employees.AsNoTracking().AnyAsync(e =>
                e.Email == email && (excludeEmployeeId == null || e.EmployeeId != excludeEmployeeId));
    }
}
