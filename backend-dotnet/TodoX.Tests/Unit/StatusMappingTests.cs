using TodoX.Api.Services;

namespace TodoX.Tests.Unit;

/// <summary>
/// api-contract §4.2 / FR-010: the UI sends "completed" but the stored status is "complete";
/// anything other than "active"/"completed" means no status condition.
/// </summary>
[Trait("Category", "Unit")]
public class StatusMappingTests
{
    [Fact]
    public void Active_MapsToActive()
    {
        Assert.Equal("active", StatusFilter.ToStatus("active"));
    }

    [Fact]
    public void Completed_MapsToComplete()
    {
        Assert.Equal("complete", StatusFilter.ToStatus("completed"));
    }

    [Theory]
    [InlineData("all")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("foo")]
    public void All_ReturnsNoStatusCondition(string? filter)
    {
        Assert.Null(StatusFilter.ToStatus(filter));
    }

    [Fact]
    public void RawComplete_TreatedAsAll()
    {
        Assert.Null(StatusFilter.ToStatus("complete"));
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("COMPLETED")]
    public void CaseSensitive(string filter)
    {
        Assert.Null(StatusFilter.ToStatus(filter));
    }
}
