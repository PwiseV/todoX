using System.Net;

namespace TodoX.Tests.Integration;

/// <summary>DELETE /api/tasks/{id} (US1), including DF-02 and the DF-03 trailing "!".</summary>
public partial class TasksControllerTests
{
    [Fact]
    public async Task DeleteTask_Existing_Returns200WithDeletedTask()
    {
        var created = await CreateTaskAsync("to delete");
        var id = created.GetProperty("_id").GetString()!;

        var response = await _client.DeleteAsync($"/api/tasks/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await ReadJsonAsync(response);
        Assert.Equal(
            ["_id", "title", "status", "completedAt", "createdAt", "updatedAt", "__v"],
            task.EnumerateObject().Select(p => p.Name));
        Assert.Equal(id, task.GetProperty("_id").GetString());
        Assert.Equal("to delete", task.GetProperty("title").GetString());
        Assert.Null(await FindInDbAsync(id));
    }

    [Fact]
    public async Task DeleteTask_Twice_Returns404_WithExclamation()
    {
        var id = (await CreateTaskAsync("to delete")).GetProperty("_id").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"/api/tasks/{id}")).StatusCode);

        var response = await _client.DeleteAsync($"/api/tasks/{id}");

        await AssertMessageAsync(response, HttpStatusCode.NotFound, "Nhiệm vụ không tồn tại!");
    }

    [Fact]
    public async Task DeleteTask_MalformedId_Returns500()
    {
        var response = await _client.DeleteAsync("/api/tasks/not-a-valid-uuid");

        await AssertMessageAsync(response, HttpStatusCode.InternalServerError, "Lỗi hệ thống");
    }
}
