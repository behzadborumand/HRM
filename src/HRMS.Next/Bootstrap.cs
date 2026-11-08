using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Next;

public static class Bootstrap
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var username = Environment.GetEnvironmentVariable("HRMS_BOOTSTRAP_USER");
        var password = Environment.GetEnvironmentVariable("HRMS_BOOTSTRAP_PASSWORD");
        var companyName = Environment.GetEnvironmentVariable("HRMS_BOOTSTRAP_COMPANY");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || password.Length < 14 || string.IsNullOrWhiteSpace(companyName))
            throw new InvalidOperationException("Bootstrap requires HRMS_BOOTSTRAP_USER, HRMS_BOOTSTRAP_PASSWORD (at least 14 characters), and HRMS_BOOTSTRAP_COMPANY.");

        await using var scope = services.CreateAsyncScope();
        var organization = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        var access = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        if (await access.Users.AnyAsync(x => x.Username == username, ct))
            throw new InvalidOperationException("Bootstrap user already exists; no changes made.");
        var company = await organization.Companies.SingleOrDefaultAsync(x => x.Name == companyName, ct);
        if (company is null)
        {
            company = new Company { Name = companyName.Trim() };
            organization.Companies.Add(company);
            await organization.SaveChangesAsync(ct);
        }
        if (!await organization.Departments.AnyAsync(x => x.CompanyId == company.Id, ct))
        {
            organization.Departments.Add(new Department { CompanyId = company.Id, Name = "عمومی" });
            await organization.SaveChangesAsync(ct);
        }
        var user = new AppUser { Username = username.Trim(), CompanyId = company.Id, IsAdmin = true, CanReadEmployees = true, CanWriteEmployees = true };
        user.PasswordHash = hasher.HashPassword(user, password);
        access.Users.Add(user);
        await access.SaveChangesAsync(ct);
    }
}
