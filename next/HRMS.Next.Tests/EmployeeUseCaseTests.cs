using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class EmployeeUseCaseTests
{
    private static EmployeeService CreateService()
    {
        var options = new DbContextOptionsBuilder<PersonnelDbContext>()
            .UseSqlServer("Server=localhost;Database=HRMS_Next_ModelTest;Integrated Security=True;TrustServerCertificate=True").Options;
        return new EmployeeService(new PersonnelDbContext(options), new UnusedDirectory(), TimeProvider.System);
    }

    [Fact]
    public async Task ReaderCannotCreateEmployee()
    {
        var actor = new ActorScope(1, "reader", 4, true, false, true, false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => CreateService().CreateAsync(actor,
            new EmployeeInput(1, "1", "نام", "خانوادگی", null, new DateOnly(2024, 1, 1)), CancellationToken.None));
    }

    [Theory]
    [InlineData("bad-email")]
    [InlineData("a@b.com;")]
    public async Task DirectUseCaseRejectsInvalidEmailBeforeDatabaseWrite(string email)
    {
        var actor = new ActorScope(1, "admin", 4, true, true, true, true);
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().CreateAsync(actor,
            new EmployeeInput(1, "1", "نام", "خانوادگی", email, new DateOnly(2024, 1, 1)), CancellationToken.None));
    }

    private sealed class UnusedDirectory : IOrganizationDirectory
    {
        public Task<bool> CompanyExistsAsync(int companyId, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> DepartmentBelongsAsync(int departmentId, int companyId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<DepartmentOption>> DepartmentsAsync(int companyId, CancellationToken ct) => throw new NotImplementedException();
    }
}
