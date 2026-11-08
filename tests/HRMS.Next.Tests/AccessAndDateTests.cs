using HRMS.Next.Modules.Access;
using HRMS.Next.Pages.Employees;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class AccessAndDateTests
{
    [Fact]
    public void CompanyScopeIsRequiredEvenForAdministrator()
    {
        var admin = new ActorScope(1, "admin", 10, true, true, false, false);
        Assert.True(AccessRules.CanRead(admin, 10));
        Assert.False(AccessRules.CanRead(admin, 11));
        Assert.False(AccessRules.CanWrite(admin, 11));
    }

    [Fact]
    public void ReadGrantDoesNotPermitWriting()
    {
        var reader = new ActorScope(2, "reader", 10, true, false, true, false);
        Assert.True(AccessRules.CanRead(reader, 10));
        Assert.False(AccessRules.CanWrite(reader, 10));
        Assert.False(AccessRules.CanRead(reader with { IsActive = false }, 10));
    }

    [Theory]
    [InlineData("۱۴۰۳/۰۱/۰۱")]
    [InlineData("١٤٠٣/٠١/٠١")]
    [InlineData("1403/01/01")]
    public void JalaliInputAcceptsAllCommonDigitSets(string input)
    {
        Assert.True(JalaliDate.TryParse(input, out var date));
        Assert.Equal(new DateOnly(2024, 3, 20), date);
        Assert.Equal("1403/01/01", JalaliDate.Format(date));
    }

    [Theory]
    [InlineData("1403/13/01")]
    [InlineData("1403/01/32")]
    [InlineData("2024-03-20")]
    public void InvalidJalaliInputIsRejected(string input) => Assert.False(JalaliDate.TryParse(input, out _));
}
