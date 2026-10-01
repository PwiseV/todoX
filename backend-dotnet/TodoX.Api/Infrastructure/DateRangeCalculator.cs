namespace TodoX.Api.Infrastructure;

/// <summary>
/// Start dates for the dateQuery filter (api-contract §4.1), computed in the configured zone.
/// </summary>
public class DateRangeCalculator(TimeProvider timeProvider, TimeZoneInfo timeZone)
{
    private const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";

    /// <summary>
    /// UTC start of the range, or null for "no date filter". Matching is case-sensitive and any
    /// value other than today/week/month (including "all") means no filter, as in the Node code.
    /// </summary>
    public DateTime? GetStartDate(string? dateQuery)
    {
        var today = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).Date;

        return dateQuery switch
        {
            "today" => ToUtc(today),
            // Weeks start on Monday: (getDay() + 6) % 7 days back, so Sunday goes back 6.
            "week" => ToUtc(today.AddDays(-(((int)today.DayOfWeek + 6) % 7))),
            "month" => ToUtc(new DateTime(today.Year, today.Month, 1)),
            _ => null,
        };
    }

    /// <summary>FR-014: the TZ environment value when set, else Asia/Ho_Chi_Minh (Node's <c>TZ ||= ...</c>).</summary>
    public static TimeZoneInfo ResolveTimeZone(string? tzEnv) =>
        TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrEmpty(tzEnv) ? DefaultTimeZoneId : tzEnv);

    // GetUtcOffset returns the standard offset for a midnight skipped by a DST jump, which lands on
    // the first valid local instant after it, as JS setHours(0, 0, 0, 0) does.
    private DateTime ToUtc(DateTime localMidnight) =>
        new DateTimeOffset(localMidnight, timeZone.GetUtcOffset(localMidnight)).UtcDateTime;
}
