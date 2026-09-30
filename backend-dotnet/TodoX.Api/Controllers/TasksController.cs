using Microsoft.AspNetCore.Mvc;
using TodoX.Api.DTOs;
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

        var update = new TaskUpdate(dto.Title, dto.Status, CompletedAtSpecified: false, CompletedAt: null);
        var task = await taskService.UpdateAsync(id, update);
        if (task is null)
        {
            return NotFound(new { message = "Nhiệm vụ không tồn tại" });
        }

        return Ok(TaskResponseDto.FromEntity(task));
    }
}
