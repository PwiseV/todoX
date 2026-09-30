using System.Net;
using System.Text.Json;

namespace TodoX.Tests.Integration;

/// <summary>POST /api/tasks (US1), including the preserved DF-01 quirk.</summary>
public partial class TasksControllerTests
{
    private const string UuidPattern = "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";
    private const string WireTimestampPattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$";

    [Fact]
    public async Task PostTask_ValidTitle_Returns201WithFullShape()
    {
        var response = await _client.PostAsync("/api/tasks", Json("""{ "title": "Đi chợ" }"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await ReadJsonAsync(response);
        Assert.Equal(
            ["_id", "title", "status", "completedAt", "createdAt", "updatedAt", "__v"],
            task.EnumerateObject().Select(p => p.Name));
        Assert.Matches(UuidPattern, task.GetProperty("_id").GetString());
        Assert.Equal("Đi chợ", task.GetProperty("title").GetString());
        Assert.Equal("active", task.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, task.GetProperty("completedAt").ValueKind);
        Assert.Equal(0, task.GetProperty("__v").GetInt32());
        var createdAt = task.GetProperty("createdAt").GetString();
        Assert.Equal(createdAt, task.GetProperty("updatedAt").GetString());
        Assert.Matches(WireTimestampPattern, createdAt);
        Assert.Matches(WireTimestampPattern, task.GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task PostTask_PaddedTitle_StoresTrimmed()
    {
        var response = await _client.PostAsync("/api/tasks", Json("""{ "title": "  x  " }"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await ReadJsonAsync(response);
        Assert.Equal("x", task.GetProperty("title").GetString());
        var stored = await FindInDbAsync(task.GetProperty("_id").GetString()!);
        Assert.Equal("x", stored!.Title);
    }

    [Fact]
    public async Task PostTask_EmptyTitle_Returns500()
    {
        var response = await _client.PostAsync("/api/tasks", Json("""{ "title": "" }"""));

        await AssertMessageAsync(response, HttpStatusCode.InternalServerError, "Lỗi hệ thống");
    }

    [Fact]
    public async Task PostTask_MissingTitle_Returns500()
    {
        var response = await _client.PostAsync("/api/tasks", Json("{}"));

        await AssertMessageAsync(response, HttpStatusCode.InternalServerError, "Lỗi hệ thống");
    }

    [Fact]
    public async Task PostTask_TabsAndNewlinesOnly_Returns500()
    {
        var response = await _client.PostAsync("/api/tasks", Json("""{ "title": "\t\n" }"""));

        await AssertMessageAsync(response, HttpStatusCode.InternalServerError, "Lỗi hệ thống");
    }

    [Fact]
    public async Task PostTask_ExtraFields_Ignored()
    {
        var response = await _client.PostAsync("/api/tasks", Json("""{ "title": "a", "status": "complete" }"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await ReadJsonAsync(response);
        Assert.Equal("active", task.GetProperty("status").GetString());
    }
}
