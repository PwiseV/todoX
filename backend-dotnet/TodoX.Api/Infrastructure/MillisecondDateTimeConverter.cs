using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TodoX.Api.Infrastructure;

/// <summary>
/// Wire format for every timestamp: ISO 8601 UTC with exactly three fractional digits
/// and a trailing Z (e.g. 2026-09-28T02:15:00.123Z). Sub-millisecond ticks are truncated.
/// </summary>
public class MillisecondDateTimeConverter : JsonConverter<DateTime>
{
    internal const string Format = "yyyy-MM-ddTHH:mm:ss.fff'Z'";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset().UtcDateTime;

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ToUtc(value).ToString(Format, CultureInfo.InvariantCulture));

    // Unspecified is treated as already-UTC: every DateTime in this app originates as UTC.
    internal static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };
}

public class NullableMillisecondDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly MillisecondDateTimeConverter Inner = new();

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : Inner.Read(ref reader, typeof(DateTime), options);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Inner.Write(writer, value.Value, options);
    }
}
