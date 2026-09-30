using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TodoX.Api.DTOs;
using TodoX.Api.Infrastructure;
using TodoX.Api.Services;

namespace TodoX.Api.Controllers;

/// <summary>
/// /api/tasks. No try/catch here: unhandled errors (DF-01 constraint violations,
/// DF-02 malformed ids) reach GlobalExceptionHandler as 500 { "message": "Lỗi hệ thống" }.
/// </summary>
[ApiController]
[Route("api/tasks")]
public class TasksController(ITaskService taskService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskDto dto)
    {
        var task = await taskService.CreateAsync(dto.Title);
        return StatusCode(StatusCodes.Status201Created, TaskResponseDto.FromEntity(task));
    }

    // {id} has no :guid constraint: a malformed id must reach Guid.Parse and become 500 (DF-02).
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateTaskDto dto)
    {
        // Title is validated before the id is parsed, as in the Node controller (research.md R-03).
        if (dto.Title is not null && string.IsNullOrWhiteSpace(dto.Title))
        {
            return BadRequest(new { message = "Tiêu đề nhiệm vụ không được để trống" });
        }

        if (dto.Status is not null && dto.Status is not ("active" or "complete"))
        {
            return BadRequest(new { message = "Dữ liệu nhiệm vụ không hợp lệ" });
        }

        if (!TryReadCompletedAt(dto.CompletedAt, out var completedAtSpecified, out var completedAt))
        {
            return BadRequest(new { message = "Dữ liệu nhiệm vụ không hợp lệ" });
        }

        var update = new TaskUpdate(dto.Title, dto.Status, completedAtSpecified, completedAt);
        var task = await taskService.UpdateAsync(id, update);
        if (task is null)
        {
            return NotFound(new { message = "Nhiệm vụ không tồn tại" });
        }

        return Ok(TaskResponseDto.FromEntity(task));
    }

    // Raw string id, as for PUT: a malformed id becomes 500 (DF-02).
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var task = await taskService.DeleteAsync(id);
        if (task is null)
        {
            // The trailing "!" differs from PUT's 404 on purpose (DF-03).
            return NotFound(new { message = "Nhiệm vụ không tồn tại!" });
        }

        return Ok(TaskResponseDto.FromEntity(task));
    }

    /// <summary>
    /// Maps the three wire states of completedAt (research.md R-01): absent leaves it unchanged,
    /// null clears it, an ISO string sets it. Returns false for an unparseable string or any other
    /// JSON kind, which the caller turns into 400 (api-contract §5.8, §8).
    /// </summary>
    private static bool TryReadCompletedAt(JsonElement element, out bool specified, out DateTime? value)
    {
        specified = element.ValueKind != JsonValueKind.Undefined;
        value = null;

        switch (element.ValueKind)
        {
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
                return true;
            case JsonValueKind.String:
                // GetDateTimeOffset, not GetDateTime: the latter returns Kind=Local for "+07:00"
                // strings, which Npgsql rejects for timestamptz (research.md R-11).
                // Only the parse is guarded, so Guid.Parse's FormatException still yields 500 (DF-02).
                try
                {
                    value = DateTimeTruncation.TruncateToMilliseconds(element.GetDateTimeOffset().UtcDateTime);
                    return true;
                }
                catch (FormatException)
                {
                    return false;
                }
            default:
                return false;
        }
    }
}
