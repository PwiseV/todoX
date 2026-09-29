using System.Text.Json.Serialization;
using TodoX.Api.Entities;

namespace TodoX.Api.DTOs;

/// <summary>
/// Wire shape of a task (api-contract §2). Property order is the serialization order.
/// </summary>
public class TaskResponseDto
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = null!;

    public string Title { get; init; } = null!;

    public string Status { get; init; } = null!;

    public DateTime? CompletedAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    /// <summary>Mongoose version key; always 0 (api-contract §6.2).</summary>
    [JsonPropertyName("__v")]
    public int V => 0;

    public static TaskResponseDto FromEntity(TaskEntity entity) => new()
    {
        Id = entity.Id.ToString(),
        Title = entity.Title,
        Status = entity.Status,
        CompletedAt = entity.CompletedAt,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };
}
