using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using TodoX.Api.Entities;
using TodoX.Tests.Integration.Fixtures;

namespace TodoX.Tests.Integration;

/// <summary>
/// Shared setup for the /api/tasks integration tests. The test methods live in
/// TasksControllerTests.*.cs partial files, one per story area.
/// </summary>
[Collection("Postgres")]
public partial class TasksControllerTests(PostgresContainerFixture db) : IAsyncLifetime
{
    private static readonly DateTimeOffset DefaultNow = new(2026, 9, 30, 5, 0, 0, TimeSpan.Zero);

    private FakeTimeProvider _clock = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await db.TruncateTasksAsync();
        _clock = new FakeTimeProvider(DefaultNow);
        _client = db.Factory.CreateClient(_clock);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>POSTs a task, then advances the clock 1 s so createdAt values stay distinct (DF-04).</summary>
    private async Task<JsonElement> CreateTaskAsync(string title)
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", new { title });
        response.EnsureSuccessStatusCode();
        var task = await ReadJsonAsync(response);
        _clock.Advance(TimeSpan.FromSeconds(1));
        return task;
    }

    /// <summary>Moves the fake clock forward. Seed tasks oldest first: the clock cannot go back.</summary>
    private void SetNow(DateTimeOffset now)
    {
        if (now < _clock.GetUtcNow())
        {
            throw new InvalidOperationException($"Clock is at {_clock.GetUtcNow():O}; cannot move back to {now:O}.");
        }

        _clock.SetUtcNow(now);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    /// <summary>Sends <paramref name="json"/> verbatim, so tests control exactly which fields are present.</summary>
    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>Asserts the status and that the body is exactly <c>{ "message": ... }</c>.</summary>
    private static async Task AssertMessageAsync(HttpResponseMessage response, HttpStatusCode status, string message)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(["message"], body.EnumerateObject().Select(p => p.Name));
        Assert.Equal(message, body.GetProperty("message").GetString());
    }

    /// <summary>Reads the stored row through a fresh context; null when it does not exist.</summary>
    private async Task<TaskEntity?> FindInDbAsync(string id)
    {
        await using var context = db.CreateDbContext();
        return await context.Tasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == Guid.Parse(id));
    }
}
