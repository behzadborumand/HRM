using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using HRMS.Next.Modules.Training;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class SqlTrainingFlowTests
{
    [Fact]
    public async Task AdminCreatesAssignsAndCompletesWithinCompany()
    {
        var connection = Environment.GetEnvironmentVariable("HRMS_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        if (!string.Equals(new SqlConnectionStringBuilder(connection).InitialCatalog,
                "HRMS_Next_Trial_20261108", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQL integration tests may run only against the disposable trial database.");
        await using var organization = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(connection).Options);
        await using var personnel = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(connection).Options);
        await using var training = new TrainingDbContext(new DbContextOptionsBuilder<TrainingDbContext>().UseSqlServer(connection).Options);
        var company = new Company { Name = "Training test " + Guid.NewGuid().ToString("N") };
        organization.Companies.Add(company);
        await organization.SaveChangesAsync();
        var department = new Department { CompanyId = company.Id, Name = "Test" };
        organization.Departments.Add(department);
        await organization.SaveChangesAsync();
        var employee = new Employee
        {
            CompanyId = company.Id, DepartmentId = department.Id,
            EmployeeNumber = "TR-" + Guid.NewGuid().ToString("N")[..12],
            FirstName = "Test", LastName = "Employee", HireDate = new DateOnly(2020, 1, 1)
        };
        personnel.Employees.Add(employee);
        await personnel.SaveChangesAsync();
        try
        {
            var actor = new ActorScope(601, "training-admin", company.Id, true, true, true, true);
            var service = new TrainingService(training, new PersonnelDirectory(personnel), TimeProvider.System);
            var courseId = await service.CreateCourseAsync(actor, "Safety", null, null, null, 365, true, CancellationToken.None);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.CreateCourseAsync(actor with { IsAdmin = false }, "Forbidden", null, null, null, null, false, CancellationToken.None));
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var assignmentId = await service.AssignAsync(actor, employee.Id, courseId, today.AddDays(-3), today.AddDays(-1), CancellationToken.None);
            Assert.Equal("Overdue", Assert.Single(await service.AssignmentsAsync(actor, CancellationToken.None)).Status);
            training.Assignments.Add(new TrainingAssignment { CompanyId = company.Id, EmployeeId = employee.Id,
                CourseId = courseId, AssignedDate = today, AssignedByUserId = actor.Id });
            await Assert.ThrowsAsync<DbUpdateException>(() => training.SaveChangesAsync());
            training.ChangeTracker.Clear();
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.AssignAsync(actor, employee.Id, courseId, today, null, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.CompleteAsync(actor with { CompanyId = company.Id + 1 }, assignmentId, today, null, CancellationToken.None));
            await service.CompleteAsync(actor, assignmentId, today, "CERT-1", CancellationToken.None);
            var row = Assert.Single(await service.AssignmentsAsync(actor, CancellationToken.None));
            Assert.Equal("Completed", row.Status);
            Assert.Equal("CERT-1", row.Assignment.CertificateNumber);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.CompleteAsync(actor, assignmentId, today, null, CancellationToken.None));
        }
        finally
        {
            training.ChangeTracker.Clear();
            training.Assignments.RemoveRange(await training.Assignments.Where(x => x.CompanyId == company.Id).ToListAsync());
            await training.SaveChangesAsync();
            training.Courses.RemoveRange(await training.Courses.Where(x => x.CompanyId == company.Id).ToListAsync());
            await training.SaveChangesAsync();
            personnel.Employees.Remove(employee);
            await personnel.SaveChangesAsync();
            organization.Departments.Remove(department);
            organization.Companies.Remove(company);
            await organization.SaveChangesAsync();
        }
    }
}
