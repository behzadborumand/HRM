using HRMS.Core.Dtos;
using HRMS.Core.Services;
using System.Globalization;
using Xunit;

namespace HRMS.Tests;

public class DashboardHighlightsTests
{
    private static DateTime Jalali(int year, int month, int day) => new PersianCalendar().ToDateTime(year, month, day, 0, 0, 0, 0);

    [Fact]
    public void Birthday_today_uses_jalali_month_and_day_without_exposing_birth_year()
    {
        var person = new DashboardPerson(1, "همکار", Jalali(1370, 8, 16), Jalali(1400, 2, 1), null, false);
        var row = Assert.Single(DashboardHighlightsRules.Celebrations(new[] { person }, Jalali(1405, 8, 16)));
        Assert.Equal(Jalali(1405, 8, 16), row.Date);
        Assert.Equal("Birthday", row.Kind);
        Assert.Null(row.Years);
    }

    [Fact]
    public void Leap_day_birthday_clamps_to_last_day_of_esfand_in_common_year()
    {
        var person = new DashboardPerson(1, "همکار", Jalali(1370, 12, 30), Jalali(1400, 2, 1), null, false);
        Assert.Equal(Jalali(1405, 12, 29), Assert.Single(DashboardHighlightsRules.Celebrations(new[] { person }, Jalali(1405, 12, 20))).Date);
    }

    [Fact]
    public void Window_wraps_jalali_year_and_excludes_day_30()
    {
        var people = new[] {
            new DashboardPerson(1, "در بازه", Jalali(1370, 1, 1), Jalali(1400, 6, 1), null, false),
            new DashboardPerson(2, "خارج بازه", Jalali(1370, 1, 21), Jalali(1400, 6, 1), null, false) };
        var rows = DashboardHighlightsRules.Celebrations(people, Jalali(1405, 12, 20));
        Assert.Equal(1, Assert.Single(rows).Person.EmployeeId);
    }

    [Fact]
    public void Anniversary_starts_after_one_complete_jalali_year()
    {
        var people = new[] {
            new DashboardPerson(1, "جدید", null, Jalali(1405, 8, 16), null, false),
            new DashboardPerson(2, "باسابقه", null, Jalali(1400, 8, 16), null, false) };
        var row = Assert.Single(DashboardHighlightsRules.Celebrations(people, Jalali(1405, 8, 16)));
        Assert.Equal(2, row.Person.EmployeeId);
        Assert.Equal("Anniversary", row.Kind);
        Assert.Equal(5, row.Years);
    }

    [Fact]
    public void New_hires_include_today_and_day_29_but_exclude_future_and_day_30()
    {
        var today = new DateTime(2026, 11, 7);
        var people = new[] {0, -29, -30, 1}.Select((offset, i) => new DashboardPerson(i, "همکار", null, today.AddDays(offset), null, false));
        Assert.Equal(new[] {0, 1}, DashboardHighlightsRules.NewHires(people, today).Select(p => p.EmployeeId));
    }

    [Fact]
    public void New_hires_are_ordered_before_limit()
    {
        var today = new DateTime(2026, 11, 7);
        var people = new[] {-15, -2, -4, 0}.Select((offset, i) => new DashboardPerson(i, "همکار", null, today.AddDays(offset), null, false));
        Assert.Equal(new[] {3, 1, 2}, DashboardHighlightsRules.NewHires(people, today).Select(p => p.EmployeeId));
    }

    [Theory]
    [InlineData("https://company.example/help", true)]
    [InlineData("http://intranet.local", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,test", false)]
    [InlineData("file:///C:/test", false)]
    [InlineData("//example.com", false)]
    [InlineData("/Settings", false)]
    [InlineData("https://user:pass@example.com", false)]
    [InlineData("https://example.com/\nheader", false)]
    [InlineData("", false)]
    public void Organization_links_accept_only_absolute_web_urls_without_credentials(string url, bool allowed)
        => Assert.Equal(allowed, OrganizationLinkRules.IsAllowedUrl(url));
}
