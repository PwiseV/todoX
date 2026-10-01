using TodoX.Api.Entities;

namespace TodoX.Api.Services;

public interface ITaskService
{
    /// <summary>One page of tasks for GET /api/tasks; query values arrive as raw strings (api-contract §4).</summary>
    Task<TaskListResult> GetTasksAsync(string? dateQuery, string? filter, string? page, string? limit);

    /// <summary>Trims the title and saves a new active task. A blank or null title fails at the DB (DF-01).</summary>
    Task<TaskEntity> CreateAsync(string? title);

    /// <summary>Applies the partial update. Returns null when no task has this id; a malformed id throws (DF-02).</summary>
    Task<TaskEntity?> UpdateAsync(string id, TaskUpdate update);

    /// <summary>Removes the task and returns it. Returns null when no task has this id; a malformed id throws (DF-02).</summary>
    Task<TaskEntity?> DeleteAsync(string id);
}

/// <summary>
/// A validated partial update. Null <see cref="Title"/>/<see cref="Status"/> mean "leave unchanged";
/// <see cref="CompletedAt"/> is applied only when <see cref="CompletedAtSpecified"/> is true, so null can clear it.
/// </summary>
/// <summary>
/// A page of tasks. <see cref="TotalCount"/> matches both dateQuery and filter; the status counts
/// cover the dateQuery range only. <see cref="Page"/> and <see cref="Limit"/> are the parsed values echoed back.
/// </summary>
public record TaskListResult(List<TaskEntity> Tasks, int TotalCount, int ActiveCount, int CompleteCount, int TotalPages, int Page, int Limit);

public record TaskUpdate(string? Title, string? Status, bool CompletedAtSpecified, DateTime? CompletedAt);
