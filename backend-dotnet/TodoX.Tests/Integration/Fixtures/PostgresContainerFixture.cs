using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TodoX.Api.Data;

namespace TodoX.Tests.Integration.Fixtures;

/// <summary>
/// One PostgreSQL 16 container per test collection, migrated once. Tests in the
/// "Postgres" collection run sequentially and share it; each test resets the Tasks table.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16").Build();

    public string ConnectionString => _container.GetConnectionString();

    public TodoXWebFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        Factory = new TodoXWebFactory(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task TruncateTasksAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Tasks\"");
    }
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>;
