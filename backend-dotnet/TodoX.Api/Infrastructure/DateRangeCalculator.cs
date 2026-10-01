namespace TodoX.Api.Infrastructure;

/// <summary>
/// Start dates for the dateQuery filter (api-contract §4.1), computed in the configured zone.
/// </summary>
public class DateRangeCalculator(TimeProvider timeProvider, TimeZoneInfo timeZone)
{
    public DateTime? GetStartDate(string? dateQuery) => throw new NotImplementedException();

    public static TimeZoneInfo ResolveTimeZone(string? tzEnv) => throw new NotImplementedException();
}
