using HRMS.Core.Services;
using Xunit;

namespace HRMS.Tests
{
    public class TimeOffRulesTests
    {
        [Theory]
        [InlineData(2026, 11, 6)]   // Friday
        [InlineData(2026, 11, 13)]  // Friday
        public void Friday_is_not_a_working_day(int year, int month, int day) =>
            Assert.False(TimeOffRules.IsWorkingDay(new DateTime(year, month, day)));

        [Theory]
        [InlineData(2026, 11, 2)]   // Sunday
        [InlineData(2026, 11, 4)]   // Tuesday
        [InlineData(2026, 11, 7)]   // Saturday
        public void Other_days_of_the_week_are_working_days(int year, int month, int day) =>
            Assert.True(TimeOffRules.IsWorkingDay(new DateTime(year, month, day)));

        [Fact]
        public void Six_calendar_days_with_one_friday_count_five_working_days() =>
            Assert.Equal(5, TimeOffRules.WorkingDaysBetween(new DateTime(2026, 11, 2), new DateTime(2026, 11, 7)));

        [Fact]
        public void A_single_friday_has_no_working_hours() =>
            Assert.Equal(0m, TimeOffRules.WorkingHours(new DateTime(2026, 11, 6), new DateTime(2026, 11, 6)));

        [Fact]
        public void Each_working_day_is_worth_eight_hours() =>
            Assert.Equal(24m, TimeOffRules.WorkingHours(new DateTime(2026, 11, 8), new DateTime(2026, 11, 10)));

        [Fact]
        public void A_range_that_ends_before_it_starts_has_no_working_days() =>
            Assert.Equal(0, TimeOffRules.WorkingDaysBetween(new DateTime(2026, 11, 10), new DateTime(2026, 11, 8)));

        [Fact]
        public void Time_inside_a_day_does_not_create_extra_days() =>
            Assert.Equal(TimeOffRules.WorkingDaysBetween(new DateTime(2026, 11, 2), new DateTime(2026, 11, 4)),
                TimeOffRules.WorkingDaysBetween(new DateTime(2026, 11, 2, 8, 30, 0), new DateTime(2026, 11, 4, 17, 0, 0)));

        [Fact]
        public void An_employee_hired_before_the_year_gets_the_full_entitlement() =>
            Assert.Equal(216m, TimeOffRules.ProrateAccrual(new DateTime(2020, 9, 19), 2026, 216m));

        [Fact]
        public void An_employee_hired_after_the_year_gets_nothing() =>
            Assert.Equal(0m, TimeOffRules.ProrateAccrual(new DateTime(2027, 2, 1), 2026, 216m));

        [Fact]
        public void A_mid_year_start_is_pro_rated_over_whole_months() =>
            Assert.Equal(126m, TimeOffRules.ProrateAccrual(new DateTime(2026, 6, 22), 2026, 216m));

        [Fact]
        public void An_unlimited_policy_accrues_no_hours_to_spend() =>
            Assert.Equal(0m, TimeOffRules.ProrateAccrual(new DateTime(2026, 6, 22), 2026, 0m));
    }
}
