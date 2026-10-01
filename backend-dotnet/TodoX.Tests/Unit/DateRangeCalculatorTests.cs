using Microsoft.Extensions.Time.Testing;
using TodoX.Api.Infrastructure;

namespace TodoX.Tests.Unit;

/// <summary>
/// api-contract §4.1 / FR-009, FR-014: start dates are local midnights in Asia/Ho_Chi_Minh (UTC+7),
/// returned as UTC. Weeks start on Monday.
/// </summary>
[Trait("Category", "Unit")]
public class DateRangeCalculatorTests
{
    // Wed 2026-09-30 12:00 ICT.
    private static readonly DateTimeOffset WednesdayNoon = new(2026, 9, 30, 5, 0, 0, TimeSpan.Zero);

    private static DateRangeCalculator At(DateTimeOffset now) =>
        new(new FakeTimeProvider(now), DateRangeCalculator.ResolveTimeZone(null));

    private static DateTime Utc(int year, int month, int day, int hour) => new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TimeZone_AsiaHoChiMinh_Resolves()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

        Assert.Equal(TimeSpan.FromHours(7), zone.BaseUtcOffset);
    }

    [Fact]
    public void ResolveTimeZone_UsesTzEnvValue_WhenSet()
    {
        var zone = DateRangeCalculator.ResolveTimeZone("UTC");

        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("UTC").Id, zone.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveTimeZone_DefaultsToHcm_WhenNullOrEmpty(string? tzEnv)
    {
        var zone = DateRangeCalculator.ResolveTimeZone(tzEnv);

        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh").Id, zone.Id);
    }

    [Fact]
    public void Today_StartsAtMidnightHcm()
    {
        var start = At(WednesdayNoon).GetStartDate("today");

        Assert.Equal(Utc(2026, 9, 29, 17), start);
        Assert.Equal(DateTimeKind.Utc, start!.Value.Kind);
    }

    [Fact]
    public void Today_JustAfterLocalMidnight()
    {
        // Mon 2026-09-28 00:30 ICT: still Sunday in UTC, but "today" is Monday locally.
        var start = At(new DateTimeOffset(2026, 9, 27, 17, 30, 0, TimeSpan.Zero)).GetStartDate("today");

        Assert.Equal(Utc(2026, 9, 27, 17), start);
    }

    [Fact]
    public void Week_StartsOnMondayHcm()
    {
        var start = At(WednesdayNoon).GetStartDate("week");

        Assert.Equal(Utc(2026, 9, 27, 17), start);
        Assert.Equal(DateTimeKind.Utc, start!.Value.Kind);
    }

    [Fact]
    public void Week_OnSunday_GoesBackToMonday()
    {
        // Sun 2026-10-04 12:00 ICT.
        var start = At(new DateTimeOffset(2026, 10, 4, 5, 0, 0, TimeSpan.Zero)).GetStartDate("week");

        Assert.Equal(Utc(2026, 9, 27, 17), start);
    }

    [Fact]
    public void Month_StartsOnFirstOfMonth()
    {
        var start = At(WednesdayNoon).GetStartDate("month");

        Assert.Equal(Utc(2026, 8, 31, 17), start);
        Assert.Equal(DateTimeKind.Utc, start!.Value.Kind);
    }

    [Theory]
    [InlineData("all")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("foo")]
    [InlineData("weeks")]
    [InlineData("TODAY")]
    public void All_ReturnsNullStartDate(string? dateQuery)
    {
        Assert.Null(At(WednesdayNoon).GetStartDate(dateQuery));
    }
}
