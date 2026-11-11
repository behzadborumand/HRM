using System.Text;

namespace HRMS.Next.Modules.Reporting;

public static class CsvReport
{
    public static byte[] ToUtf8(string[] headers, IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', headers.Select(Escape)));
        foreach (var row in rows) builder.AppendLine(string.Join(',', row.Select(Escape)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private static string Escape(string? value)
    {
        value ??= "";
        var leadingTrimmed = value.TrimStart(' ', '\t', '\r', '\n');
        if (leadingTrimmed.Length > 0 && "=+-@".Contains(leadingTrimmed[0])) value = "'" + value;
        if (value.IndexOfAny([',', '"', '\r', '\n']) >= 0)
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
