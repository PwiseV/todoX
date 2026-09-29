using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using TodoX.Api.Data;

namespace TodoX.Tests.Integration.Fixtures;

/// <summary>
/// Runs the real API pipeline in-process against the Testcontainers database.
/// </summary>
public sealed class TodoXWebFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Drop Program.cs's registration (options + its configuration callback) and point at the container.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        });
    }

    /// <summary>
    /// A client whose server uses <paramref name="clock"/> as its TimeProvider, so each test
    /// controls "now". Derived factories are disposed together with this factory.
    /// </summary>
    public HttpClient CreateClient(FakeTimeProvider clock) =>
        WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock)))
            .CreateClient();
}
