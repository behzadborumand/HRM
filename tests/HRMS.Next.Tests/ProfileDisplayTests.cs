using System.Globalization;
using HRMS.Next.Pages.Employees;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class ProfileDisplayTests
{
    [Theory]
    [InlineData("1234.50", "1,234.5")]
    [InlineData("1234.56", "1,234.56")]
    [InlineData("1000", "1,000")]
    [InlineData(null, "—")]
    public void ContractRateKeepsItsStoredFraction(string? input, string expected)
    {
        decimal? value = input is null ? null : decimal.Parse(input, CultureInfo.InvariantCulture);
        Assert.Equal(expected, ProfileDisplay.PayRate(value));
    }

    [Theory]
    [InlineData("2024-01-01", "2024-01-03", "2026-01-01", 2)]
    [InlineData("2024-01-01", "2027-01-01", "2024-01-04", 3)]
    [InlineData("2024-01-01", null, "2024-01-05", 4)]
    [InlineData("2025-01-01", null, "2024-01-01", 0)]
    public void EmploymentDaysEndsAtDepartureOrToday(string hired, string? terminated, string today, int expected)
    {
        Assert.Equal(expected, ProfileDisplay.EmploymentDays(DateOnly.Parse(hired, CultureInfo.InvariantCulture),
            terminated is null ? null : DateOnly.Parse(terminated, CultureInfo.InvariantCulture),
            DateOnly.Parse(today, CultureInfo.InvariantCulture)));
    }
}
