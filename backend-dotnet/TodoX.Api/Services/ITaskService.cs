using TodoX.Api.Entities;

namespace TodoX.Api.Services;

public interface ITaskService
{
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
public record TaskUpdate(string? Title, string? Status, bool CompletedAtSpecified, DateTime? CompletedAt);
