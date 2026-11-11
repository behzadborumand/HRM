using System.Security.Claims;
using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlAccountAdministrationTests
{
    [Fact]
    public async Task AdminCanLinkAndManageOnlySameCompanyAccountsWithAudit()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog, "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Integration tests may run only against the disposable trial database.");
        await using var org = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
        await using var people = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options);
        await using var access = new AccessDbContext(new DbContextOptionsBuilder<AccessDbContext>().UseSqlServer(connection).Options);
        var company = new Company { Name = "Account " + Guid.NewGuid().ToString("N") };
        var foreign = new Company { Name = "Foreign account " + Guid.NewGuid().ToString("N") };
        org.Companies.AddRange(company, foreign);
        await org.SaveChangesAsync();
        var department = new Department { CompanyId = company.Id, Name = "First" };
        var otherDepartment = new Department { CompanyId = foreign.Id, Name = "Other" };
        org.Departments.AddRange(department, otherDepartment);
        await org.SaveChangesAsync();
        var employee = new Employee { CompanyId = company.Id, DepartmentId = department.Id, EmployeeNumber = "AC-" + Guid.NewGuid().ToString("N")[..12], FirstName = "First", LastName = "Person", HireDate = new DateOnly(2024, 1, 1) };
        var outsider = new Employee { CompanyId = foreign.Id, DepartmentId = otherDepartment.Id, EmployeeNumber = "AC-" + Guid.NewGuid().ToString("N")[..12], FirstName = "Other", LastName = "Person", HireDate = new DateOnly(2024, 1, 1) };
        people.Employees.AddRange(employee, outsider);
        await people.SaveChangesAsync();
        try
        {
            var actor = new ActorScope(91, "admin", company.Id, true, true, true, true);
            var service = new AccountAdministrationService(access, new PersonnelDirectory(people), new PasswordHasher<AppUser>(), TimeProvider.System);
            var username = "new_" + Guid.NewGuid().ToString("N")[..12];
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(actor,
                new AccountCreateInput(username, "LongPassword123!", outsider.Id, true, false), CancellationToken.None));
            var id = await service.CreateAsync(actor,
                new AccountCreateInput(username, "LongPassword123!", employee.Id, true, false), CancellationToken.None);
            var account = await access.Users.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(employee.Id, account.EmployeeId);
            var auth = new AccessService(access, new PasswordHasher<AppUser>());
            var validPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim(AccessService.AuthVersionClaim, Convert.ToBase64String(account.RowVersion))
            ], "Cookies"));
            var invalidPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim(AccessService.AuthVersionClaim, Convert.ToBase64String([1, 2]))
            ], "Cookies"));
            Assert.NotNull(await auth.GetActiveAsync(validPrincipal, CancellationToken.None));
            Assert.Null(await auth.GetActiveAsync(invalidPrincipal, CancellationToken.None));
            Assert.Equal(PasswordVerificationResult.Success,
                new PasswordHasher<AppUser>().VerifyHashedPassword(account, account.PasswordHash, "LongPassword123!"));
            Assert.Single((await service.ListAsync(actor, 1, CancellationToken.None)).Rows);
            Assert.Empty((await service.ListAsync(actor with { CompanyId = foreign.Id }, 1, CancellationToken.None)).Rows);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListAsync(actor with { IsAdmin = false }, 1, CancellationToken.None));
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdateAsync(actor, id,
                new AccountUpdateInput(employee.Id, true, true, false), [1, 2], CancellationToken.None));
            await service.UpdateAsync(actor, id, new AccountUpdateInput(employee.Id, true, true, false), account.RowVersion, CancellationToken.None);
            Assert.False((await access.Users.AsNoTracking().SingleAsync(x => x.Id == id)).IsActive);
            await service.ResetPasswordAsync(actor, id, "NewLongPassword123!", CancellationToken.None);
            var afterReset = await access.Users.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(PasswordVerificationResult.Failed, new PasswordHasher<AppUser>().VerifyHashedPassword(afterReset, afterReset.PasswordHash, "LongPassword123!"));
            Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<AppUser>().VerifyHashedPassword(afterReset, afterReset.PasswordHash, "NewLongPassword123!"));
            Assert.Equal(3, await access.Audits.CountAsync(x => x.CompanyId == company.Id && x.TargetUserId == id));
        }
        finally
        {
            access.ChangeTracker.Clear();
            access.Audits.RemoveRange(await access.Audits.Where(x => x.CompanyId == company.Id).ToListAsync());
            access.Users.RemoveRange(await access.Users.Where(x => x.CompanyId == company.Id).ToListAsync());
            await access.SaveChangesAsync();
            people.Employees.RemoveRange(employee, outsider);
            await people.SaveChangesAsync();
            org.Departments.RemoveRange(department, otherDepartment);
            org.Companies.RemoveRange(company, foreign);
            await org.SaveChangesAsync();
        }
    }
}
