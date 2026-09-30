using TodoX.Api.Data;
using TodoX.Api.Entities;
using TodoX.Api.Infrastructure;

namespace TodoX.Api.Services;

public class TaskService(AppDbContext db, TimeProvider timeProvider) : ITaskService
{
    public async Task<TaskEntity> CreateAsync(string? title)
    {
        var now = Now();
        var task = new TaskEntity
        {
            // A null title stays null so the NOT NULL constraint fails; blank trims to "" and fails
            // the CHECK. Either way the DbUpdateException reaches the global handler as 500 (DF-01).
            Title = title?.Trim()!,
            Status = "active",
            CompletedAt = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    public Task<TaskEntity?> UpdateAsync(string id, TaskUpdate update) => throw new NotImplementedException();

    public Task<TaskEntity?> DeleteAsync(string id) => throw new NotImplementedException();

    private DateTime Now() => DateTimeTruncation.TruncateToMilliseconds(timeProvider.GetUtcNow().UtcDateTime);
}
