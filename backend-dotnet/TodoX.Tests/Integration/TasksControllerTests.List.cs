using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace TodoX.Tests.Integration;

/// <summary>
/// GET /api/tasks (US2): dateQuery, filter, sort, pagination. Counts (activeCount/completeCount)
/// belong to US3 and are not asserted here.
/// </summary>
public partial class TasksControllerTests
{
    // "Now" for every list test: Wed 2026-09-30 12:00 ICT. Today starts 2026-09-29T17:00Z,
    // the week 2026-09-27T17:00Z (Mon) and the month 2026-08-31T17:00Z.
    private static readonly DateTimeOffset ListNow = new(2026, 9, 30, 5, 0, 0, TimeSpan.Zero);

    /// <summary>The seeded tasks, keyed A-G. B and D are complete; the rest are active. F and G are optional.</summary>
    private sealed record Seeded(string A, string B, string C, string D, string E, string? F, string? G);

    /// <summary>
    /// Seeds oldest first (the clock only moves forward), completes B and D, then sets now to <see cref="ListNow"/>.
    /// A: last month. B: this month, an earlier week. C: this week (Mon). D, E (and F, G): today.
    /// </summary>
    private async Task<Seeded> SeedListAsync(bool withExtraToday = false)
    {
        // The shared clock starts at ListNow; seeding needs earlier dates, so restart it before the oldest task.
        RestartClockAt(new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        var a = await CreateAtAsync("A", new(2026, 8, 15, 3, 0, 0, TimeSpan.Zero));
        var b = await CreateAtAsync("B", new(2026, 9, 10, 3, 0, 0, TimeSpan.Zero));
        var c = await CreateAtAsync("C", new(2026, 9, 28, 2, 0, 0, TimeSpan.Zero));
        var d = await CreateAtAsync("D", new(2026, 9, 30, 1, 0, 0, TimeSpan.Zero));
        var e = await CreateAtAsync("E", new(2026, 9, 30, 2, 0, 0, TimeSpan.Zero));
        string? f = null, g = null;
        if (withExtraToday)
        {
            f = await CreateAtAsync("F", new(2026, 9, 30, 3, 0, 0, TimeSpan.Zero));
            g = await CreateAtAsync("G", new(2026, 9, 30, 4, 0, 0, TimeSpan.Zero));
        }

        foreach (var id in new[] { b, d })
        {
            var response = await PutAsync(id, """{ "status": "complete", "completedAt": "2026-09-30T02:30:00.000Z" }""");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        SetNow(ListNow);
        return new Seeded(a, b, c, d, e, f, g);
    }

    /// <summary>Replaces the clock and the client with ones starting at <paramref name="start"/>.</summary>
    private void RestartClockAt(DateTimeOffset start)
    {
        _client.Dispose();
        _clock = new FakeTimeProvider(start);
        _client = db.Factory.CreateClient(_clock);
    }

    private async Task<string> CreateAtAsync(string title, DateTimeOffset createdAt)
    {
        SetNow(createdAt);
        return (await CreateTaskAsync(title)).GetProperty("_id").GetString()!;
    }

    private async Task<JsonElement> GetListAsync(string query = "")
    {
        var response = await _client.GetAsync($"/api/tasks{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static string[] Ids(JsonElement list) =>
        list.GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("_id").GetString()!).ToArray();

    [Fact]
    public async Task GetTasks_DateQuery_Today()
    {
        var s = await SeedListAsync();

        var list = await GetListAsync("?dateQuery=today");

        Assert.Equal(new[] { s.D, s.E }.Order(), Ids(list).Order());
    }

    [Fact]
    public async Task GetTasks_DateQuery_Week()
    {
        var s = await SeedListAsync();

        var list = await GetListAsync("?dateQuery=week");

        Assert.Equal(new[] { s.C, s.D, s.E }.Order(), Ids(list).Order());
    }

    [Fact]
    public async Task GetTasks_DateQuery_Month()
    {
        var s = await SeedListAsync();

        var list = await GetListAsync("?dateQuery=month");

        Assert.Equal(new[] { s.B, s.C, s.D, s.E }.Order(), Ids(list).Order());
    }

    [Theory]
    [InlineData("?dateQuery=all")]
    [InlineData("")]
    [InlineData("?dateQuery=foo")]
    public async Task GetTasks_DateQuery_All(string query)
    {
        var s = await SeedListAsync();

        var list = await GetListAsync(query);

        Assert.Equal(new[] { s.A, s.B, s.C, s.D, s.E }.Order(), Ids(list).Order());
    }

    [Fact]
    public async Task GetTasks_Filter_Active()
    {
        var s = await SeedListAsync();

        var list = await GetListAsync("?filter=active");

        Assert.Equal(new[] { s.A, s.C, s.E }.Order(), Ids(list).Order());
        Assert.All(list.GetProperty("tasks").EnumerateArray(), t => Assert.Equal("active", t.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task GetTasks_Filter_Completed()
    {
        var s = await SeedListAsync();

        var list = await GetListAsync("?filter=completed");

        Assert.Equal(new[] { s.B, s.D }.Order(), Ids(list).Order());
        Assert.All(list.GetProperty("tasks").EnumerateArray(), t => Assert.Equal("complete", t.GetProperty("status").GetString()));
    }

    [Theory]
    [InlineData("?filter=all")]
    [InlineData("")]
    [InlineData("?filter=complete")] // the stored value, not the UI value: no filter
    public async Task GetTasks_Filter_All(string query)
    {
        var s = await SeedListAsync();

        var list = await GetListAsync(query);

        Assert.Equal(new[] { s.A, s.B, s.C, s.D, s.E }.Order(), Ids(list).Order());
    }

    [Fact]
    public async Task GetTasks_Sort_ActiveFirst()
    {
        await SeedListAsync();

        var list = await GetListAsync();

        var statuses = list.GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("status").GetString()!).ToArray();
        Assert.Equal(["active", "active", "active", "complete", "complete"], statuses);
    }

    [Fact]
    public async Task GetTasks_Sort_NewestFirstWithinGroup()
    {
        var s = await SeedListAsync();

        var list = await GetListAsync();

        Assert.Equal([s.E, s.C, s.A, s.D, s.B], Ids(list));
    }

    [Fact]
    public async Task GetTasks_Pagination_SecondPageSlice()
    {
        // F and G (active, today) make 7: sort order is G, F, E, C, A (active), then D, B.
        var s = await SeedListAsync(withExtraToday: true);

        var page1 = await GetListAsync("?limit=5&page=1");
        var page2 = await GetListAsync("?limit=5&page=2");

        Assert.Equal([s.G!, s.F!, s.E, s.C, s.A], Ids(page1));
        Assert.Equal([s.D, s.B], Ids(page2));
        Assert.Equal(2, page2.GetProperty("page").GetInt32());
        Assert.Equal(7, page2.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page2.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task GetTasks_PageZero_TreatedAsPage1()
    {
        var s = await SeedListAsync();

        var list = await GetListAsync("?page=0&limit=2");

        Assert.Equal(1, list.GetProperty("page").GetInt32());
        Assert.Equal([s.E, s.C], Ids(list));
    }

    [Fact]
    public async Task GetTasks_NonNumericPageAndLimit_Returns200WithDefaults()
    {
        await SeedListAsync();

        // GetListAsync asserts 200: string binding, not an automatic 400 from int model binding.
        var list = await GetListAsync("?page=abc&limit=xyz");

        Assert.Equal(1, list.GetProperty("page").GetInt32());
        Assert.Equal(5, list.GetProperty("limit").GetInt32());
        Assert.Equal(5, list.GetProperty("tasks").GetArrayLength());
    }

    [Fact]
    public async Task GetTasks_LimitClampedAndEchoed()
    {
        await SeedListAsync();

        var list = await GetListAsync("?limit=1000");

        Assert.Equal(50, list.GetProperty("limit").GetInt32());
        Assert.Equal(1, list.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task GetTasks_PageBeyondTotal_EchoesFarPage()
    {
        await SeedListAsync();

        var list = await GetListAsync("?page=9999");

        Assert.Empty(Ids(list));
        Assert.Equal(9999, list.GetProperty("page").GetInt32());
        Assert.Equal(1, list.GetProperty("totalPages").GetInt32());
        Assert.Equal(5, list.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task GetTasks_EmptyDb_TotalPagesIsOne()
    {
        SetNow(ListNow);

        var list = await GetListAsync();

        Assert.Empty(Ids(list));
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, list.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, list.GetProperty("page").GetInt32());
        Assert.Equal(5, list.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task GetTasks_TotalCount_RespectsBothFilters()
    {
        var s = await SeedListAsync();

        var list = await GetListAsync("?dateQuery=week&filter=active");

        // This week: C, D, E; active among them: C, E.
        Assert.Equal(2, list.GetProperty("totalCount").GetInt32());
        Assert.Equal([s.E, s.C], Ids(list));
    }

    [Fact]
    public async Task GetTasks_ResponseShape()
    {
        await SeedListAsync();

        var list = await GetListAsync();

        Assert.Equal(
            ["tasks", "activeCount", "completeCount", "totalCount", "totalPages", "page", "limit"],
            list.EnumerateObject().Select(p => p.Name));
        Assert.Equal(JsonValueKind.Array, list.GetProperty("tasks").ValueKind);
        foreach (var key in new[] { "activeCount", "completeCount", "totalCount", "totalPages", "page", "limit" })
        {
            Assert.Equal(JsonValueKind.Number, list.GetProperty(key).ValueKind);
        }

        var task = list.GetProperty("tasks")[0];
        Assert.Equal(
            ["_id", "title", "status", "completedAt", "createdAt", "updatedAt", "__v"],
            task.EnumerateObject().Select(p => p.Name));
        Assert.Matches(WireTimestampPattern, task.GetProperty("createdAt").GetString());
    }
}
