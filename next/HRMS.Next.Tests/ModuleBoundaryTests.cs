using HRMS.Next.Modules.Access;
using HRMS.Next.Modules.Organization;
using HRMS.Next.Modules.Personnel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class ModuleBoundaryTests
{
    private const string DummyConnection = "Server=localhost;Database=HRMS_Next_ModelTest;Integrated Security=True;TrustServerCertificate=True";

    [Fact]
    public void ModuleContextsOwnDisjointEntitiesAndSchemas()
    {
        using var access = new AccessDbContext(new DbContextOptionsBuilder<AccessDbContext>().UseSqlServer(DummyConnection).Options);
        using var organization = new OrganizationDbContext(new DbContextOptionsBuilder<OrganizationDbContext>().UseSqlServer(DummyConnection).Options);
        using var personnel = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(DummyConnection).Options);
        var contexts = new[] { access.Model, organization.Model, personnel.Model };
        var all = contexts.SelectMany(x => x.GetEntityTypes()).ToList();
        Assert.Equal(all.Count, all.Select(x => x.ClrType).Distinct().Count());
        Assert.All(access.Model.GetEntityTypes(), x => Assert.Equal("access", x.GetSchema()));
        Assert.All(organization.Model.GetEntityTypes(), x => Assert.Equal("organization", x.GetSchema()));
        Assert.All(personnel.Model.GetEntityTypes(), x => Assert.Equal("personnel", x.GetSchema()));
        Assert.All(all, x => Assert.All(x.GetForeignKeys(), fk => Assert.Equal(x.GetSchema(), fk.PrincipalEntityType.GetSchema())));
    }

    [Fact]
    public void EmployeeDatesAndConcurrencyMapToSqlServerTypes()
    {
        using var personnel = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseSqlServer(DummyConnection).Options);
        var employee = personnel.Model.FindEntityType(typeof(Employee))!;
        Assert.Equal("date", employee.FindProperty(nameof(Employee.HireDate))!.GetColumnType());
        Assert.Equal("date", employee.FindProperty(nameof(Employee.TerminationDate))!.GetColumnType());
        Assert.True(employee.FindProperty(nameof(Employee.RowVersion))!.IsConcurrencyToken);
    }
}
