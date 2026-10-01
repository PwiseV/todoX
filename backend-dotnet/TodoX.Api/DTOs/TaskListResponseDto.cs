namespace TodoX.Api.DTOs;

/// <summary>
/// GET /api/tasks body (contracts/tasks-api.md). Property order is the serialization order.
/// </summary>
public class TaskListResponseDto
{
    public TaskResponseDto[] Tasks { get; init; } = [];

    /// <summary>Active tasks in the dateQuery range, ignoring filter.</summary>
    public int ActiveCount { get; init; }

    /// <summary>Complete tasks in the dateQuery range, ignoring filter.</summary>
    public int CompleteCount { get; init; }

    /// <summary>Tasks matching both dateQuery and filter.</summary>
    public int TotalCount { get; init; }

    public int TotalPages { get; init; }

    public int Page { get; init; }

    public int Limit { get; init; }
}
