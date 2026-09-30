using System.Net;
using System.Text.Json;

namespace TodoX.Tests.Integration;

/// <summary>PUT /api/tasks/{id} completedAt handling (FR-003, research.md R-01, R-11): absent vs null vs string.</summary>
public partial class TasksControllerTests
{
    private const string CompletedAtIso = "2026-09-28T03:00:00.000Z";
    private static readonly DateTime CompletedAtUtc = new(2026, 9, 28, 3, 0, 0, DateTimeKind.Utc);

    /// <summary>Creates a task and completes it with the frontend's Complete payload; asserts it took effect.</summary>
    private async Task<string> CreateCompletedTaskAsync()
    {
        var id = (await CreateTaskAsync("to complete")).GetProperty("_id").GetString()!;
        var response = await PutAsync(id, $$"""{ "status": "complete", "completedAt": "{{CompletedAtIso}}" }""");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CompletedAtIso, (await ReadJsonAsync(response)).GetProperty("completedAt").GetString());
        return id;
    }

    [Fact]
    public async Task PutTask_FrontendCompletePayload_PersistsUtc()
    {
        var id = (await CreateTaskAsync("task")).GetProperty("_id").GetString()!;

        var response = await PutAsync(id, $$"""{ "status": "complete", "completedAt": "{{CompletedAtIso}}" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CompletedAtIso, (await ReadJsonAsync(response)).GetProperty("completedAt").GetString());
        var stored = (await FindInDbAsync(id))!.CompletedAt;
        Assert.NotNull(stored);
        Assert.Equal(DateTimeKind.Utc, stored.Value.Kind);
        Assert.Equal(CompletedAtUtc, stored.Value);
    }

    [Fact]
    public async Task PutTask_CompletedAtWithSubMs_TruncatedToMs()
    {
        var id = (await CreateTaskAsync("task")).GetProperty("_id").GetString()!;

        var response = await PutAsync(id, """{ "status": "complete", "completedAt": "2026-09-28T03:00:00.1239Z" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("2026-09-28T03:00:00.123Z", (await ReadJsonAsync(response)).GetProperty("completedAt").GetString());
        Assert.Equal(CompletedAtUtc.AddMilliseconds(123), (await FindInDbAsync(id))!.CompletedAt);
    }

    [Fact]
    public async Task PutTask_CompletedAtWithOffset_StoredAsUtc()
    {
        var id = (await CreateTaskAsync("task")).GetProperty("_id").GetString()!;

        var response = await PutAsync(id, """{ "status": "complete", "completedAt": "2026-09-28T10:00:00.000+07:00" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CompletedAtIso, (await ReadJsonAsync(response)).GetProperty("completedAt").GetString());
        var stored = (await FindInDbAsync(id))!.CompletedAt;
        Assert.NotNull(stored);
        Assert.Equal(DateTimeKind.Utc, stored.Value.Kind);
        Assert.Equal(CompletedAtUtc, stored.Value);
    }

    [Fact]
    public async Task PutTask_ReopenPayload_ClearsCompletedAt()
    {
        var id = await CreateCompletedTaskAsync();

        var response = await PutAsync(id, """{ "status": "active", "completedAt": null }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await ReadJsonAsync(response);
        Assert.Equal("active", task.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, task.GetProperty("completedAt").ValueKind);
        Assert.Null((await FindInDbAsync(id))!.CompletedAt);
    }

    [Fact]
    public async Task PutTask_RenameOnly_LeavesCompletedAtUnchanged()
    {
        var id = await CreateCompletedTaskAsync();

        var response = await PutAsync(id, """{ "title": "renamed" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await ReadJsonAsync(response);
        Assert.Equal("renamed", task.GetProperty("title").GetString());
        Assert.Equal(CompletedAtIso, task.GetProperty("completedAt").GetString());
        Assert.Equal(CompletedAtUtc, (await FindInDbAsync(id))!.CompletedAt);
    }

    [Fact]
    public async Task PutTask_InvalidCompletedAt_Returns400_WithInvalidDataMessage()
    {
        var id = (await CreateTaskAsync("task")).GetProperty("_id").GetString()!;

        var response = await PutAsync(id, """{ "status": "complete", "completedAt": "not-a-date" }""");

        await AssertMessageAsync(response, HttpStatusCode.BadRequest, InvalidDataMessage);
        var stored = (await FindInDbAsync(id))!;
        Assert.Equal("active", stored.Status);
        Assert.Null(stored.CompletedAt);
    }
}
