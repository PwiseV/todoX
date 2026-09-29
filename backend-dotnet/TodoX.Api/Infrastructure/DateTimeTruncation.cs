namespace TodoX.Api.Infrastructure;

public static class DateTimeTruncation
{
    /// <summary>
    /// Drops sub-millisecond ticks so the stored timestamptz (microsecond precision)
    /// matches the 3-fractional-digit wire format exactly.
    /// </summary>
    public static DateTime TruncateToMilliseconds(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, value.Kind);
}
