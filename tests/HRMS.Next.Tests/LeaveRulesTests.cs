using HRMS.Next.Modules.Leave;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class LeaveRulesTests
{
    [Fact]
    public void WorkingHoursExcludesFridayButIncludesSaturday()
    {
        Assert.Equal(16m, LeaveRules.WorkingHours(new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 7)));
        Assert.Equal(0m, LeaveRules.WorkingHours(new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 6)));
    }

    [Fact]
    public void AccrualIsProratedByWholeMonthsOfService()
    {
        Assert.Equal(120m, LeaveRules.ProrateAccrual(new DateOnly(2026, 7, 15), 2026, 240m));
        Assert.Equal(240m, LeaveRules.ProrateAccrual(new DateOnly(2025, 12, 1), 2026, 240m));
        Assert.Equal(0m, LeaveRules.ProrateAccrual(new DateOnly(2027, 1, 1), 2026, 240m));
    }

    [Fact]
    public void RangeCannotPrecedeHireOrExceedTermination()
    {
        var hire = new DateOnly(2026, 1, 10);
        var end = new DateOnly(2026, 12, 15);
        Assert.Throws<ArgumentException>(() => LeaveRules.ValidateRange(new DateOnly(2026, 1, 11), hire, hire, end));
        Assert.Throws<ArgumentException>(() => LeaveRules.ValidateRange(new DateOnly(2026, 12, 16), new DateOnly(2026, 12, 17), hire, end));
        Assert.Throws<ArgumentException>(() => LeaveRules.ValidateRange(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 1), hire, end));
        LeaveRules.ValidateRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 2), hire, end);
    }
}