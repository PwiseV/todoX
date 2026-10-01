namespace TodoX.Api.Services;

/// <summary>page/limit parsing and paging math (api-contract §4.3), matching the Node parseInt rules.</summary>
public static class Pagination
{
    private const int DefaultPage = 1;
    private const int DefaultLimit = 5;
    private const int MaxLimit = 50;

    /// <summary><c>Math.max(1, parseInt(page) || 1)</c>; pages beyond the last are not clamped.</summary>
    public static int ParsePage(string? page) =>
        (int)Math.Max(1, OrDefault(ParseLeadingInt(page), DefaultPage));

    /// <summary><c>Math.min(50, Math.max(1, parseInt(limit) || 5))</c>, so "0" gives 5 (DF-05).</summary>
    public static int ParseLimit(string? limit) =>
        (int)Math.Clamp(OrDefault(ParseLeadingInt(limit), DefaultLimit), 1, MaxLimit);

    /// <summary><c>max(1, ceil(totalCount / limit))</c>.</summary>
    public static int TotalPages(int totalCount, int limit) =>
        Math.Max(1, (totalCount + limit - 1) / limit);

    /// <summary><c>(page - 1) * limit</c>, saturated so a far page gives an empty slice instead of overflowing.</summary>
    public static int Skip(int page, int limit) =>
        (int)Math.Min(int.MaxValue, (long)(page - 1) * limit);

    // JS `x || fallback`: NaN (null here) and 0 are falsy. Capped at int.MaxValue so callers can cast.
    private static long OrDefault(long? value, int fallback) =>
        value is null or 0 ? fallback : Math.Min(value.Value, int.MaxValue);

    /// <summary>
    /// JS parseInt(s, 10): skips leading whitespace, takes an optional sign, then reads digits up to
    /// the first non-digit. No digits means NaN (null). The magnitude saturates instead of overflowing.
    /// </summary>
    private static long? ParseLeadingInt(string? s)
    {
        if (s is null)
        {
            return null;
        }

        var i = 0;
        while (i < s.Length && char.IsWhiteSpace(s[i]))
        {
            i++;
        }

        var negative = false;
        if (i < s.Length && s[i] is '+' or '-')
        {
            negative = s[i] == '-';
            i++;
        }

        var start = i;
        long value = 0;
        while (i < s.Length && char.IsAsciiDigit(s[i]))
        {
            value = Math.Min(value * 10 + (s[i] - '0'), int.MaxValue + 1L);
            i++;
        }

        if (i == start)
        {
            return null;
        }

        return negative ? -value : value;
    }
}
