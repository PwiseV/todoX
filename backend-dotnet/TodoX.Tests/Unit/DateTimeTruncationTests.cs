using TodoX.Api.Infrastructure;

namespace TodoX.Tests.Unit;

[Trait("Category", "Unit")]
public class DateTimeTruncationTests
{
    private static readonly DateTime WholeMs = new(2026, 9, 28, 10, 0, 0, 1, DateTimeKind.Utc);

    [Fact]
    public void TruncateToMilliseconds_DropsSubMillisecondTicks()
    {
        var value = WholeMs.AddTicks(9_999); // 0.9999 ms extra

        var truncated = DateTimeTruncation.TruncateToMilliseconds(value);

        Assert.Equal(WholeMs, truncated);
        Assert.Equal(0, truncated.Ticks % TimeSpan.TicksPerMillisecond);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void TruncateToMilliseconds_PreservesKind(DateTimeKind kind)
    {
        var value = DateTime.SpecifyKind(WholeMs.AddTicks(1), kind);

        var truncated = DateTimeTruncation.TruncateToMilliseconds(value);

        Assert.Equal(kind, truncated.Kind);
    }

    [Fact]
    public void TruncateToMilliseconds_AlreadyTruncated_IsUnchanged()
    {
        var truncated = DateTimeTruncation.TruncateToMilliseconds(WholeMs);

        Assert.Equal(WholeMs.Ticks, truncated.Ticks);
    }
}
