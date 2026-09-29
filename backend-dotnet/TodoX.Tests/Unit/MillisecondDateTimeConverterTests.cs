using System.Text.Json;
using System.Text.RegularExpressions;
using TodoX.Api.Infrastructure;

namespace TodoX.Tests.Unit;

[Trait("Category", "Unit")]
public class MillisecondDateTimeConverterTests
{
    private static readonly Regex WireFormat = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");

    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new MillisecondDateTimeConverter(), new NullableMillisecondDateTimeConverter() },
    };

    private sealed record Holder(DateTime? At);

    private static string Unquote(string json) => JsonSerializer.Deserialize<string>(json)!;

    [Fact]
    public void Serialize_SevenDigitTicks_TruncatesToThreeDigits()
    {
        var value = new DateTime(2026, 9, 28, 2, 15, 0, DateTimeKind.Utc).AddTicks(1_234_567);

        var json = JsonSerializer.Serialize(value, Options);

        Assert.Equal("\"2026-09-28T02:15:00.123Z\"", json);
    }

    [Fact]
    public void Serialize_WholeSecond_WritesThreeZeroDigits()
    {
        var value = new DateTime(2026, 9, 28, 2, 15, 0, DateTimeKind.Utc);

        var json = JsonSerializer.Serialize(value, Options);

        Assert.Equal("\"2026-09-28T02:15:00.000Z\"", json);
    }

    [Fact]
    public void Serialize_LocalKind_ConvertsToUtcFirst()
    {
        var local = new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Local);
        var expected = local.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fff") + "Z";

        var json = JsonSerializer.Serialize(local, Options);

        Assert.Equal(expected, Unquote(json));
    }

    [Fact]
    public void Serialize_NullableWithValue_UsesSameFormat()
    {
        DateTime? value = new DateTime(2026, 9, 28, 3, 0, 0, DateTimeKind.Utc).AddTicks(9_999);

        var json = JsonSerializer.Serialize(value, Options);

        Assert.Equal("\"2026-09-28T03:00:00.000Z\"", json);
    }

    [Fact]
    public void Serialize_NullableNull_WritesJsonNull_NotOmitted()
    {
        var json = JsonSerializer.Serialize(new Holder(null), Options);

        Assert.Equal("{\"At\":null}", json);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(9_999_999L)]
    [InlineData(5_000_000L)]
    public void Serialize_AnyValue_MatchesWireFormatRegex(long extraTicks)
    {
        var value = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(extraTicks);

        var nonNullable = Unquote(JsonSerializer.Serialize(value, Options));
        var nullable = Unquote(JsonSerializer.Serialize((DateTime?)value, Options));

        Assert.Matches(WireFormat, nonNullable);
        Assert.Matches(WireFormat, nullable);
    }

    [Fact]
    public void Deserialize_IsoZString_ReturnsUtcKind()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-09-28T03:00:00.000Z\"", Options);

        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(new DateTime(2026, 9, 28, 3, 0, 0, DateTimeKind.Utc), value);
    }

    [Fact]
    public void Deserialize_NullableNull_ReturnsNull()
    {
        var holder = JsonSerializer.Deserialize<Holder>("{\"At\":null}", Options);

        Assert.Null(holder!.At);
    }
}
