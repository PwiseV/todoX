using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TodoX.Api.Infrastructure;

namespace TodoX.Tests.Unit;

[Trait("Category", "Unit")]
public class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception> Exceptions => new()
    {
        new FormatException("Unrecognized Guid format."),
        new DbUpdateException("violates check constraint"),
        new InvalidOperationException("boom"),
    };

    [Theory]
    [MemberData(nameof(Exceptions))]
    public async Task TryHandleAsync_AnyException_Writes500WithGenericMessage(Exception exception)
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        var properties = body.RootElement.EnumerateObject().ToList();
        var message = Assert.Single(properties);
        Assert.Equal("message", message.Name);
        Assert.Equal("Lỗi hệ thống", message.Value.GetString());
    }
}
