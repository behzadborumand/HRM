using HRMS.Next.Modules.Documents;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class DocumentRulesTests
{
    [Theory]
    [InlineData("contract.pdf")]
    [InlineData("scan.JPG")]
    [InlineData("report.docx")]
    [InlineData("archive.zip")]
    public void AcceptsLegacyDocumentExtensions(string name)
    {
        Assert.Null(DocumentRules.Validate("Contract", null, name, 1024));
    }

    [Theory]
    [InlineData("payload.exe", 1024)]
    [InlineData("script.bat", 1024)]
    [InlineData("no_extension", 1024)]
    [InlineData("C:secret.pdf", 1024)]
    [InlineData("bad	name.pdf", 1024)]
    [InlineData("empty.pdf", 0)]
    [InlineData("oversize.pdf", 10485761)]
    public void RejectsUnsafeOrInvalidUpload(string name, long size)
    {
        Assert.NotNull(DocumentRules.Validate("Contract", null, name, size));
    }

    [Fact]
    public void EnforcesMetadataLimitsAndExactlyTenMegabytes()
    {
        Assert.Null(DocumentRules.Validate("Contract", "HR", "contract.pdf", 10485760));
        Assert.NotNull(DocumentRules.Validate(" ", null, "contract.pdf", 42));
        Assert.NotNull(DocumentRules.Validate(new string('x', 201), null, "contract.pdf", 42));
        Assert.NotNull(DocumentRules.Validate("Contract", new string('x', 51), "contract.pdf", 42));
    }
}
