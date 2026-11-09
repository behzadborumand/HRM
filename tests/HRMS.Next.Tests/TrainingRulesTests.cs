using System.Globalization;
using HRMS.Next.Modules.Training;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class TrainingRulesTests
{
    [Theory]
    [InlineData(null, null, 2026, 11, 9, "Pending")]
    [InlineData(null, "2026-11-08", 2026, 11, 9, "Overdue")]
    [InlineData("2026-11-07", "2026-11-08", 2026, 11, 9, "Completed")]
    public void StatusComesFromDates(string? completed, string? due, int year, int month, int day, string expected)
    {
        static DateOnly? Parse(string? value) => value is null ? null : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        Assert.Equal(expected, TrainingRules.ResolveStatus(Parse(completed), Parse(due), new DateOnly(year, month, day)));
    }

    [Fact]
    public void RenewalAndAssignmentDatesAreValidated()
    {
        Assert.Equal(new DateOnly(2027, 1, 1), TrainingRules.NextDueDate(new DateOnly(2026, 1, 1), 365));
        Assert.Throws<ArgumentException>(() => TrainingRules.ValidateCourse(" ", null));
        Assert.Throws<ArgumentException>(() => TrainingRules.ValidateCourse("Course", 0));
        Assert.Throws<ArgumentException>(() => TrainingRules.ValidateAssignment(
            new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 8), null, new DateOnly(2026, 11, 9)));
        Assert.Throws<ArgumentException>(() => TrainingRules.ValidateAssignment(
            new DateOnly(2026, 11, 1), null, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 9)));
    }
}
