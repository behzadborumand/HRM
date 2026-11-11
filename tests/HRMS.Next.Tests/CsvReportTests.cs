using System.Text;
using HRMS.Next.Modules.Reporting;
using Xunit;

namespace HRMS.Next.Tests;

public sealed class CsvReportTests
{
    [Fact]
    public void PersianCsvHasBomQuotesCommasAndNeutralizesSpreadsheetFormulas()
    {
        var bytes = CsvReport.ToUtf8(["نام"], [["=1+1"], ["name, value"]]);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.Contains("'=1+1", text);
        Assert.Contains("\"name, value\"", text);
    }
}
