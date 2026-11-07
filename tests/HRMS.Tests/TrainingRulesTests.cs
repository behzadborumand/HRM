using HRMS.Core.Services;
using Xunit;

namespace HRMS.Tests
{
    public class TrainingRulesTests
    {
        private static readonly DateTime Today = new DateTime(2026, 11, 4, 13, 30, 0);

        [Theory]
        [InlineData(-1, TrainingRules.StatusOverdue)]
        [InlineData(5, TrainingRules.StatusPending)]
        public void An_open_assignment_is_pending_or_overdue(int dueOffset, string expected)
        {
            Assert.Equal(expected, TrainingRules.ResolveStatus(null, Today.Date.AddDays(dueOffset), Today));
        }

        [Fact]
        public void No_due_date_never_reads_as_overdue()
        {
            Assert.Equal(TrainingRules.StatusPending, TrainingRules.ResolveStatus(null, null, Today));
            Assert.Equal(TrainingRules.StatusPending, TrainingRules.ResolveStatus(null, null, Today.AddDays(400)));
        }

        [Fact]
        public void A_due_date_of_today_is_not_overdue_yet()
        {
            Assert.Equal(TrainingRules.StatusPending, TrainingRules.ResolveStatus(null, Today.Date, Today));
            Assert.Equal(TrainingRules.StatusOverdue, TrainingRules.ResolveStatus(null, Today.Date.AddDays(-1), Today));
        }

        [Fact]
        public void Completion_wins_even_after_the_due_date_passed()
        {
            var overdue = Today.AddDays(-20);
            Assert.Equal(TrainingRules.StatusCompleted, TrainingRules.ResolveStatus(Today.AddDays(-2), overdue, Today));
        }

        [Fact]
        public void The_time_part_of_the_due_date_is_ignored()
        {
            // ۱۸:۰۰ امروز هنوز گذشته نیست، حتی اگر ساعت جاری ۱۳:۳۰ باشد و برعکس.
            Assert.Equal(TrainingRules.StatusPending, TrainingRules.ResolveStatus(null, Today.Date.AddHours(18), Today));
        }

        [Fact]
        public void A_renewable_course_gets_its_next_due_date_from_completion()
        {
            var completed = Today.Date;
            Assert.Equal(completed.AddDays(365), TrainingRules.NextDueDate(completed, 365));
            Assert.Null(TrainingRules.NextDueDate(completed, null));
            Assert.Null(TrainingRules.NextDueDate(null, 365));
        }

        [Fact]
        public void An_assignment_without_a_due_date_inherits_the_renewal_period()
        {
            Assert.Equal(Today.Date.AddDays(730), TrainingRules.DefaultDueDate(Today, 730));
            Assert.Null(TrainingRules.DefaultDueDate(Today, null));
            Assert.Null(TrainingRules.DefaultDueDate(Today, 0));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(4000)]
        public void A_broken_renewal_period_is_rejected(int renewalDays)
        {
            Assert.NotNull(TrainingRules.ValidateCourse("ایمنی", renewalDays));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void A_course_needs_a_title(string? title)
        {
            Assert.NotNull(TrainingRules.ValidateCourse(title, null));
        }

        [Fact]
        public void A_course_title_over_the_limit_is_rejected()
        {
            Assert.NotNull(TrainingRules.ValidateCourse(new string('ا', TrainingRules.MaxTitleLength + 1), null));
        }

        [Fact]
        public void A_plain_course_title_passes()
        {
            Assert.Null(TrainingRules.ValidateCourse("ایمنی و بهداشت محیط کار", 365));
            Assert.Null(TrainingRules.ValidateCourse(new string('ا', TrainingRules.MaxTitleLength), null));
        }

        [Fact]
        public void A_due_date_before_the_assignment_is_rejected()
        {
            var assigned = Today.Date;
            Assert.NotNull(TrainingRules.ValidateAssignment(assigned, assigned.AddDays(-1), null, Today));
            Assert.Null(TrainingRules.ValidateAssignment(assigned, assigned, null, Today));
        }

        [Fact]
        public void Training_cannot_be_recorded_in_the_future()
        {
            Assert.NotNull(TrainingRules.ValidateAssignment(Today.Date, null, Today.AddDays(3), Today));
        }

        [Fact]
        public void Training_cannot_be_completed_before_it_was_assigned()
        {
            Assert.NotNull(TrainingRules.ValidateAssignment(Today.Date, Today.AddDays(10), Today.AddDays(-30), Today));
        }

        [Fact]
        public void A_backdated_record_with_a_later_due_date_is_accepted()
        {
            Assert.Null(TrainingRules.ValidateAssignment(Today.AddDays(-40), Today.AddDays(20), Today.AddDays(-2), Today));
        }

        [Theory]
        [InlineData(-6)]
        [InlineData(0)]
        [InlineData(12)]
        public void Days_remaining_counts_whole_days(int offset)
        {
            Assert.Equal(offset, TrainingRules.DaysRemaining(Today.Date.AddDays(offset), Today.Date.AddHours(9)));
        }

        [Fact]
        public void Remaining_days_are_null_without_a_deadline()
        {
            Assert.Null(TrainingRules.DaysRemaining(null, Today));
        }

        [Fact]
        public void Every_status_the_rules_produce_has_a_persian_label_and_a_badge()
        {
            foreach (var status in TrainingRules.AllStatuses)
            {
                var label = HRMS.Web.Helpers.FaLabels.TrainingStatus(status);
                var badge = HRMS.Web.Helpers.FaLabels.TrainingStatusBadge(status);

                Assert.False(string.IsNullOrWhiteSpace(label));
                Assert.NotEqual(status, label);
                Assert.False(string.IsNullOrWhiteSpace(badge));
            }
        }
    }
}
