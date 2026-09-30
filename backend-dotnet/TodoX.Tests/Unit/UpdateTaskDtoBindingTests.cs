using System.Text.Json;
using TodoX.Api.DTOs;

namespace TodoX.Tests.Unit;

/// <summary>
/// Guards research.md R-01: completedAt must bind to three distinguishable states.
/// Uses the MVC default serializer options (JsonSerializerDefaults.Web).
/// </summary>
[Trait("Category", "Unit")]
public class UpdateTaskDtoBindingTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static UpdateTaskDto Deserialize(string json) => JsonSerializer.Deserialize<UpdateTaskDto>(json, Options)!;

    [Fact]
    public void Deserialize_AbsentCompletedAt_IsUndefined()
    {
        var dto = Deserialize("""{ "title": "x" }""");

        Assert.Equal(JsonValueKind.Undefined, dto.CompletedAt.ValueKind);
    }

    [Fact]
    public void Deserialize_NullCompletedAt_IsNull()
    {
        var dto = Deserialize("""{ "completedAt": null }""");

        Assert.Equal(JsonValueKind.Null, dto.CompletedAt.ValueKind);
    }

    [Theory]
    [InlineData("2026-09-28T03:00:00.000Z")]
    [InlineData("2026-09-28T10:00:00.000+07:00")]
    public void Deserialize_StringCompletedAt_IsString(string completedAt)
    {
        var dto = Deserialize($$"""{ "completedAt": "{{completedAt}}" }""");

        Assert.Equal(JsonValueKind.String, dto.CompletedAt.ValueKind);
        Assert.Equal(completedAt, dto.CompletedAt.GetString());
    }
}
