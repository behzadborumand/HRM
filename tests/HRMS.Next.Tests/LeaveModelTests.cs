using HRMS.Next.Modules.Leave;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class LeaveModelTests
{
    [Fact]
    public void LeaveOwnsItsEntitiesAndSqlSchema()
    {
        using var db = NewContext();
        var types = db.Model.GetEntityTypes().ToList();
        Assert.Equal(3, types.Count);
        Assert.All(types, entity => Assert.Equal("leave", entity.GetSchema()));
        Assert.All(types, entity => Assert.All(entity.GetForeignKeys(), fk => Assert.Equal("leave", fk.PrincipalEntityType.GetSchema())));
    }

    [Fact]
    public void BalanceHasUniqueBusinessKeyAndConcurrencyToken()
    {
        using var db = NewContext();
        var balance = db.Model.FindEntityType(typeof(LeaveBalance))!;
        Assert.Contains(balance.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId", "EmployeeId", "PolicyId", "Year" }));
        Assert.True(balance.FindProperty(nameof(LeaveBalance.RowVersion))!.IsConcurrencyToken);
        Assert.Equal("decimal(10,2)", balance.FindProperty(nameof(LeaveBalance.UsedHours))!.GetColumnType());
    }

    [Fact]
    public void RequestDatesAreSqlDatesAndDecisionIsConcurrencyProtected()
    {
        using var db = NewContext();
        var request = db.Model.FindEntityType(typeof(LeaveRequest))!;
        Assert.Equal("date", request.FindProperty(nameof(LeaveRequest.StartDate))!.GetColumnType());
        Assert.Equal("date", request.FindProperty(nameof(LeaveRequest.EndDate))!.GetColumnType());
        Assert.True(request.FindProperty(nameof(LeaveRequest.RowVersion))!.IsConcurrencyToken);
    }

    private static LeaveDbContext NewContext() => new(new DbContextOptionsBuilder<LeaveDbContext>()
        .UseSqlServer("Server=localhost;Database=HRMS_Next_ModelTest;Integrated Security=True;TrustServerCertificate=True").Options);
}