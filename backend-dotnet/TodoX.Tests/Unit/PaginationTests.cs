using TodoX.Api.Services;

namespace TodoX.Tests.Unit;

/// <summary>
/// api-contract §4.3 / FR-011: mirrors Math.max(1, parseInt(page) || 1) and
/// Math.min(50, Math.max(1, parseInt(limit) || 5)), including the DF-05 quirk (limit=0 gives 5).
/// </summary>
[Trait("Category", "Unit")]
public class PaginationTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData("", 1)]
    [InlineData("abc", 1)]
    [InlineData("0", 1)]
    [InlineData("-5", 1)]
    [InlineData("3", 3)]
    [InlineData("2abc", 2)]
    [InlineData(" 4", 4)]
    [InlineData("1.9", 1)]
    public void Page_Defaults_And_MinClamp(string? page, int expected)
    {
        Assert.Equal(expected, Pagination.ParsePage(page));
    }

    [Fact]
    public void Page_AboveTotal_NotClamped()
    {
        Assert.Equal(9999, Pagination.ParsePage("9999"));
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData("abc", 5)]
    [InlineData("0", 5)] // DF-05: JS 0 || 5
    [InlineData("-3", 1)]
    [InlineData("1000", 50)]
    [InlineData("51", 50)]
    [InlineData("50", 50)]
    [InlineData("7", 7)]
    public void Limit_Clamped_To_Range(string? limit, int expected)
    {
        Assert.Equal(expected, Pagination.ParseLimit(limit));
    }

    [Theory]
    [InlineData(0, 5, 1)]
    [InlineData(5, 5, 1)]
    [InlineData(6, 5, 2)]
    [InlineData(11, 5, 3)]
    public void TotalPages_AtLeastOne(int totalCount, int limit, int expected)
    {
        Assert.Equal(expected, Pagination.TotalPages(totalCount, limit));
    }

    [Fact]
    public void Skip_IsPageMinusOneTimesLimit()
    {
        Assert.Equal(10, Pagination.Skip(3, 5));
    }

    [Fact]
    public void Skip_HugePage_DoesNotOverflow()
    {
        // A far page must still give an empty slice (200), not a negative skip (500).
        Assert.Equal(int.MaxValue, Pagination.Skip(int.MaxValue, 50));
    }
}
