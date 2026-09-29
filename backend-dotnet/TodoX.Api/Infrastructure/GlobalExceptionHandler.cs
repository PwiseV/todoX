using Microsoft.AspNetCore.Diagnostics;

namespace TodoX.Api.Infrastructure;

/// <summary>
/// FR-015: every unhandled exception becomes 500 { "message": "Lỗi hệ thống" }.
/// This is also how the preserved quirks DF-01 (DbUpdateException) and DF-02 (FormatException) surface.
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await httpContext.Response.WriteAsJsonAsync(new { message = "Lỗi hệ thống" }, cancellationToken);
        return true;
    }
}
