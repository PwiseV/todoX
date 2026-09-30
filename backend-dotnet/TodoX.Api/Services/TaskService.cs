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

    public async Task<TaskEntity?> UpdateAsync(string id, TaskUpdate update)
    {
        // Guid.Parse throws FormatException for a malformed id; left unhandled on purpose (DF-02).
        var task = await db.Tasks.FindAsync(Guid.Parse(id));
        if (task is null)
        {
            return null;
        }

        if (update.Title is not null)
        {
            task.Title = update.Title.Trim();
        }

        if (update.Status is not null)
        {
            task.Status = update.Status;
        }

        if (update.CompletedAtSpecified)
        {
            task.CompletedAt = update.CompletedAt;
        }

        task.UpdatedAt = Now();
        await db.SaveChangesAsync();
        return task;
    }

    public async Task<TaskEntity?> DeleteAsync(string id)
    {
        // Guid.Parse throws FormatException for a malformed id; left unhandled on purpose (DF-02).
        var task = await db.Tasks.FindAsync(Guid.Parse(id));
        if (task is null)
        {
            return null;
        }

        db.Tasks.Remove(task);
        await db.SaveChangesAsync();
        return task;
    }

    private DateTime Now() => DateTimeTruncation.TruncateToMilliseconds(timeProvider.GetUtcNow().UtcDateTime);
}
