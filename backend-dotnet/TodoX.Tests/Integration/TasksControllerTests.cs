using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
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
}
