namespace TodoX.Api.DTOs;

/// <summary>
/// POST /api/tasks body. Only <c>title</c> is read; other fields are ignored.
/// Deliberately has no validation attributes: a missing or blank title must reach the
/// database constraint and surface as 500, not as [ApiController]'s automatic 400 (DF-01).
/// </summary>
public class CreateTaskDto
{
    public string? Title { get; init; }
}
