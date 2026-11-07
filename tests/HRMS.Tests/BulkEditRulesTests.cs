using HRMS.Core.Services;
using Xunit;

namespace HRMS.Tests
{
    public class BulkEditRulesTests
    {
        [Fact]
        public void An_empty_form_requests_no_change()
        {
            Assert.False(BulkEditRules.HasAnyChange(null, null, null, null, null));
            Assert.False(BulkEditRules.HasAnyChange(null, null, null, "  ", ""));
        }

        [Fact]
        public void A_status_only_change_leaves_the_job_untouched()
        {
            Assert.True(BulkEditRules.HasAnyChange(null, null, null, null, "OnLeave"));
            Assert.False(BulkEditRules.ChangesJob(null, null, null, null));
        }

        [Theory]
        [InlineData(4, null, null, null)]
        [InlineData(null, 7, null, null)]
        [InlineData(null, null, 2, null)]
        [InlineData(null, null, null, "Contract")]
        public void Any_job_field_selected_means_the_job_changes(int? departmentId, int? jobTitleId, int? managerId,
            string? employmentType)
        {
            Assert.True(BulkEditRules.ChangesJob(departmentId, jobTitleId, managerId, employmentType));
        }

        [Fact]
        public void An_employee_never_becomes_its_own_manager()
        {
            Assert.Null(BulkEditRules.ManagerFor(9, 9));
            Assert.Equal(4, BulkEditRules.ManagerFor(4, 9));
            Assert.Null(BulkEditRules.ManagerFor(null, 9));
        }
    }
}
