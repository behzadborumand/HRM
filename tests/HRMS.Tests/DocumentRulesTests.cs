using HRMS.Core.Services;
using Xunit;

namespace HRMS.Tests
{
    public class DocumentRulesTests
    {
        [Theory]
        [InlineData("contract.pdf")]
        [InlineData("scan.JPG")]
        [InlineData("report.docx")]
        [InlineData("archive.zip")]
        public void An_allowed_extension_passes(string fileName)
        {
            Assert.True(DocumentRules.IsAllowedExtension(fileName));
            Assert.Null(DocumentRules.Validate(fileName, 1024));
        }

        [Theory]
        [InlineData("payload.exe")]
        [InlineData("script.bat")]
        [InlineData("no_extension")]
        public void An_unexpected_extension_is_rejected(string fileName)
        {
            Assert.False(DocumentRules.IsAllowedExtension(fileName));
            Assert.NotNull(DocumentRules.Validate(fileName, 1024));
        }

        [Fact]
        public void The_size_ceiling_is_enforced()
        {
            Assert.NotNull(DocumentRules.Validate("big.pdf", DocumentRules.MaxFileSizeBytes + 1));
            Assert.NotNull(DocumentRules.Validate("empty.pdf", 0));
            Assert.Null(DocumentRules.Validate("ok.pdf", DocumentRules.MaxFileSizeBytes));
        }

        [Fact]
        public void A_missing_file_name_is_reported()
        {
            Assert.NotNull(DocumentRules.Validate(null, 1024));
            Assert.NotNull(DocumentRules.Validate("   ", 1024));
        }
    }
}
