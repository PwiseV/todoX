using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using TodoX.Api.Entities;
using TodoX.Api.Services;
using TodoX.Tests.Integration.Fixtures;

namespace TodoX.Tests.Integration;

/// <summary>
/// Service-level tests against the Testcontainers database (no in-memory provider is approved).
/// Each test reads back through a fresh context so it sees what was stored, not EF's tracked copy.
/// </summary>
[Collection("Postgres")]
public class TaskServiceTests(PostgresContainerFixture db) : IAsyncLifetime
{
    // 05:00:00.123456 UTC: the sub-millisecond part must be dropped before saving.
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 30, 5, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);
    private static readonly DateTime NowTruncated = new(2026, 9, 30, 5, 0, 0, 123, DateTimeKind.Utc);

    private readonly FakeTimeProvider _clock = new(Now);

    public Task InitializeAsync() => db.TruncateTasksAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_TrimsTitle_BeforeSave()
    {
        await using var context = db.CreateDbContext();
        var service = new TaskService(context, _clock);

        var created = await service.CreateAsync("  x  ");

        Assert.Equal("x", created.Title);
        Assert.Equal("x", (await ReadAsync(created.Id)).Title);
    }

    [Fact]
    public async Task Rename_TrimsTitle_BeforeSave()
    {
        var seeded = await SeedAsync("original");
        await using var context = db.CreateDbContext();
        var service = new TaskService(context, _clock);

        var updated = await service.UpdateAsync(seeded.Id.ToString(), new TaskUpdate("  y  ", null, false, null));

        Assert.NotNull(updated);
        Assert.Equal("y", updated.Title);
        Assert.Equal("y", (await ReadAsync(seeded.Id)).Title);
    }

    [Fact]
    public async Task Create_SetsDefaults()
    {
        await using var context = db.CreateDbContext();
        var service = new TaskService(context, _clock);

        var created = await service.CreateAsync("defaults");
        var stored = await ReadAsync(created.Id);

        Assert.Equal("active", stored.Status);
        Assert.Null(stored.CompletedAt);
        Assert.Equal(NowTruncated, stored.CreatedAt);
        Assert.Equal(NowTruncated, stored.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, stored.UpdatedAt.Kind);
    }

    private async Task<TaskEntity> SeedAsync(string title)
    {
        await using var context = db.CreateDbContext();
        var entity = new TaskEntity { Title = title, CreatedAt = NowTruncated, UpdatedAt = NowTruncated };
        context.Tasks.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    private async Task<TaskEntity> ReadAsync(Guid id)
    {
        await using var context = db.CreateDbContext();
        return await context.Tasks.AsNoTracking().SingleAsync(t => t.Id == id);
    }
}
