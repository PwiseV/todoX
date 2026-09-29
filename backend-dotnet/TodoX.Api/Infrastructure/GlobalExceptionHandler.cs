using Microsoft.AspNetCore.Diagnostics;

namespace TodoX.Api.Infrastructure;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) =>
        ValueTask.FromResult(false);
}
