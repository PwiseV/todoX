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
}
