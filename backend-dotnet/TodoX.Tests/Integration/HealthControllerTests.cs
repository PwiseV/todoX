using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using TodoX.Tests.Integration.Fixtures;

namespace TodoX.Tests.Integration;

[Collection("Postgres")]
public class HealthControllerTests(PostgresContainerFixture db)
{
    [Fact]
    public async Task GetHealth_ReturnsOkAndMillisecondTimestamp()
    {
        // 02:15:00.123456 UTC — the extra microseconds must be truncated on the wire.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 2, 15, 0, TimeSpan.Zero).AddTicks(1_234_560));
        using var client = db.Factory.CreateClient(clock);

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var properties = body.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["status", "time"], properties);
        Assert.Equal("ok", body.RootElement.GetProperty("status").GetString());
        var time = body.RootElement.GetProperty("time").GetString();
        Assert.Equal("2026-09-28T02:15:00.123Z", time);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", time);
    }
}
