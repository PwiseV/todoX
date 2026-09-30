using System.Text.Json;

namespace TodoX.Api.DTOs;

/// <summary>
/// PUT /api/tasks/{id} body; every field is optional. No validation attributes:
/// the controller validates explicitly so the contract's own 400 messages are returned.
/// </summary>
public class UpdateTaskDto
{
    /// <summary>Null or absent means "leave unchanged".</summary>
    public string? Title { get; init; }

    /// <summary>Null or absent means "leave unchanged".</summary>
    public string? Status { get; init; }

    /// <summary>
    /// Deliberately a non-nullable <see cref="JsonElement"/> (research.md R-01): an absent field
    /// keeps <c>default</c> (<see cref="JsonValueKind.Undefined"/>), while an explicit JSON null
    /// is <see cref="JsonValueKind.Null"/>. <c>JsonElement?</c> would collapse both to null.
    /// </summary>
    public JsonElement CompletedAt { get; init; }
}
