using System.Net;
using System.Text.Json;

namespace TodoX.Tests.Integration;

/// <summary>
/// GET /api/tasks badge counts (US3, api-contract §4.5, FR-012): activeCount/completeCount cover the
/// dateQuery range only and never change with filter.
/// </summary>
public partial class TasksControllerTests
{
    // Inside "today" for ListNow (today starts 2026-09-29T17:00Z), and two dates before it.
    private static readonly DateTimeOffset EarlyToday = new(2026, 9, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EarlierThisMonth = new(2026, 9, 10, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EarlierThisWeek = new(2026, 9, 28, 2, 0, 0, TimeSpan.Zero);

    private async Task CompleteAsync(string id)
    {
        var response = await PutAsync(id, """{ "status": "complete", "completedAt": "2026-09-30T00:30:00.000Z" }""");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Creates a task at <paramref name="createdAt"/>, completing it when <paramref name="complete"/> is set.</summary>
    private async Task SeedAtAsync(DateTimeOffset createdAt, bool complete)
    {
        var id = await CreateAtAsync(complete ? "complete" : "active", createdAt);
        if (complete)
        {
            await CompleteAsync(id);
        }
    }

    private static void AssertCounts(JsonElement list, int active, int complete)
    {
        Assert.Equal(active, list.GetProperty("activeCount").GetInt32());
        Assert.Equal(complete, list.GetProperty("completeCount").GetInt32());
    }

    [Fact]
    public async Task GetTasks_Counts_IndependentOfFilter()
    {
        RestartClockAt(EarlyToday);
        for (var i = 0; i < 8; i++)
        {
            // Each create advances the clock 1 s, so all 8 stay inside today.
            await SeedAtAsync(_clock.GetUtcNow(), complete: i >= 3);
        }

        SetNow(ListNow);

        foreach (var (filter, totalCount) in new[] { ("all", 8), ("active", 3), ("completed", 5), ("foo", 8) })
        {
            var list = await GetListAsync($"?dateQuery=today&filter={filter}");

            // The filter is applied to the list (totalCount) but not to the badges.
            Assert.Equal(totalCount, list.GetProperty("totalCount").GetInt32());
            AssertCounts(list, active: 3, complete: 5);
        }
    }

    [Fact]
    public async Task GetTasks_Counts_RespectDateQuery()
    {
        RestartClockAt(EarlierThisMonth);
        await SeedAtAsync(EarlierThisMonth, complete: false);
        await SeedAtAsync(EarlierThisMonth.AddMinutes(1), complete: true);
        await SeedAtAsync(EarlyToday, complete: false);
        await SeedAtAsync(EarlyToday.AddMinutes(1), complete: true);
        SetNow(ListNow);

        AssertCounts(await GetListAsync("?dateQuery=today"), active: 1, complete: 1);
        AssertCounts(await GetListAsync("?dateQuery=all"), active: 2, complete: 2);
    }

    [Fact]
    public async Task GetTasks_Counts_AllRange_IncludesEverything()
    {
        // A, C, E active; B, D complete; spread from last month to today.
        await SeedListAsync();

        AssertCounts(await GetListAsync("?dateQuery=all"), active: 3, complete: 2);
        AssertCounts(await GetListAsync(), active: 3, complete: 2);
    }

    [Fact]
    public async Task GetTasks_Counts_ZeroInOneRange_NonZeroInAnother()
    {
        RestartClockAt(EarlierThisMonth);
        await SeedAtAsync(EarlierThisMonth, complete: false);
        await SeedAtAsync(EarlierThisWeek, complete: true);
        SetNow(ListNow);

        AssertCounts(await GetListAsync("?dateQuery=today"), active: 0, complete: 0);
        AssertCounts(await GetListAsync("?dateQuery=all"), active: 1, complete: 1);
    }
}
