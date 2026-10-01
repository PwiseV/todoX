using Microsoft.EntityFrameworkCore;
using TodoX.Api.Data;
using TodoX.Api.Infrastructure;
using TodoX.Api.Services;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // camelCase is the web default; nulls are always written (no DefaultIgnoreCondition).
        options.JsonSerializerOptions.Converters.Add(new MillisecondDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableMillisecondDateTimeConverter());
    });
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TodoX")));
builder.Services.AddSingleton(TimeProvider.System);
// FR-014: resolved once at startup.
builder.Services.AddSingleton(DateRangeCalculator.ResolveTimeZone(Environment.GetEnvironmentVariable("TZ")));
builder.Services.AddSingleton<DateRangeCalculator>();
builder.Services.AddScoped<ITaskService, TaskService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(builder.Configuration["Cors:AllowedOrigin"]!)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// Configure the HTTP request pipeline.

app.UseExceptionHandler();

if (!app.Environment.IsProduction())
{
    app.UseCors(FrontendCorsPolicy);
}

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
