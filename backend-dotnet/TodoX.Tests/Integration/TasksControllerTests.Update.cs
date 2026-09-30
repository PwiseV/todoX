using System.Net;

namespace TodoX.Tests.Integration;

/// <summary>PUT /api/tasks/{id} (US1): validation, rename, status, 404, and the DF-02 quirk.</summary>
public partial class TasksControllerTests
{
    private const string BlankTitleMessage = "Tiêu đề nhiệm vụ không được để trống";
    private const string InvalidDataMessage = "Dữ liệu nhiệm vụ không hợp lệ";

    private Task<HttpResponseMessage> PutAsync(string id, string json) => _client.PutAsync($"/api/tasks/{id}", Json(json));

    [Fact]
    public async Task PutTask_ValidRename_Returns200()
    {
        var created = await CreateTaskAsync("before");
        var id = created.GetProperty("_id").GetString()!;

        var response = await PutAsync(id, """{ "title": "after" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await ReadJsonAsync(response);
        Assert.Equal("after", task.GetProperty("title").GetString());
        Assert.Equal(created.GetProperty("createdAt").GetString(), task.GetProperty("createdAt").GetString());
        // CreateTaskAsync advanced the clock 1 s, so updatedAt is the new "now", not createdAt.
        Assert.Equal("2026-09-30T05:00:01.000Z", task.GetProperty("updatedAt").GetString());
        Assert.NotEqual(task.GetProperty("createdAt").GetString(), task.GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task PutTask_PaddedTitle_StoresTrimmed()
    {
        var id = (await CreateTaskAsync("before")).GetProperty("_id").GetString()!;

        var response = await PutAsync(id, """{ "title": "  Padded task  " }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Padded task", (await ReadJsonAsync(response)).GetProperty("title").GetString());
        Assert.Equal("Padded task", (await FindInDbAsync(id))!.Title);
    }

    [Fact]
    public async Task PutTask_BlankTitle_Returns400()
    {
        var id = (await CreateTaskAsync("before")).GetProperty("_id").GetString()!;

        var response = await PutAsync(id, """{ "title": "   " }""");

        await AssertMessageAsync(response, HttpStatusCode.BadRequest, BlankTitleMessage);
    }

    [Fact]
    public async Task PutTask_EmptyStringTitle_Returns400()
    {
        var id = (await CreateTaskAsync("before")).GetProperty("_id").GetString()!;

        var response = await PutAsync(id, """{ "title": "" }""");

        await AssertMessageAsync(response, HttpStatusCode.BadRequest, BlankTitleMessage);
    }

    [Fact]
    public async Task PutTask_InvalidStatus_Returns400()
    {
        var id = (await CreateTaskAsync("before")).GetProperty("_id").GetString()!;

        var response = await PutAsync(id, """{ "status": "done" }""");

        await AssertMessageAsync(response, HttpStatusCode.BadRequest, InvalidDataMessage);
        Assert.Equal("active", (await FindInDbAsync(id))!.Status);
    }

    [Fact]
    public async Task PutTask_StatusComplete_Returns200()
    {
        var id = (await CreateTaskAsync("before")).GetProperty("_id").GetString()!;

        var response = await PutAsync(id, """{ "status": "complete" }""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("complete", (await ReadJsonAsync(response)).GetProperty("status").GetString());
        Assert.Equal("complete", (await FindInDbAsync(id))!.Status);
    }

    [Fact]
    public async Task PutTask_NonExistentId_Returns404_NoExclamation()
    {
        var response = await PutAsync(Guid.NewGuid().ToString(), """{ "title": "x" }""");

        await AssertMessageAsync(response, HttpStatusCode.NotFound, "Nhiệm vụ không tồn tại");
    }

    [Fact]
    public async Task PutTask_MalformedId_ValidTitle_Returns500()
    {
        var response = await PutAsync("not-a-valid-uuid", """{ "title": "x" }""");

        await AssertMessageAsync(response, HttpStatusCode.InternalServerError, "Lỗi hệ thống");
    }

    [Fact]
    public async Task PutTask_MalformedId_BlankTitle_Returns400()
    {
        var response = await PutAsync("not-a-valid-uuid", """{ "title": "   " }""");

        await AssertMessageAsync(response, HttpStatusCode.BadRequest, BlankTitleMessage);
    }
}
