using Microsoft.EntityFrameworkCore;
using TodoX.Api.Data;
using TodoX.Api.Entities;
using TodoX.Api.Infrastructure;

namespace TodoX.Api.Services;

public class TaskService(AppDbContext db, TimeProvider timeProvider, DateRangeCalculator dateRange) : ITaskService
{
    public async Task<TaskListResult> GetTasksAsync(string? dateQuery, string? filter, string? page, string? limit)
    {
        var start = dateRange.GetStartDate(dateQuery);
        var status = StatusFilter.ToStatus(filter);
        var pageNumber = Pagination.ParsePage(page);
        var pageSize = Pagination.ParseLimit(limit);

        // No end bound: everything from the start date on matches (api-contract §4.1).
        IQueryable<TaskEntity> inRange = db.Tasks.AsNoTracking();
        if (start is { } from)
        {
            inRange = inRange.Where(t => t.CreatedAt >= from);
        }

        var filtered = status is null ? inRange : inRange.Where(t => t.Status == status);

        // Awaited one at a time: a DbContext cannot run queries concurrently (research.md R-07).
        var tasks = await filtered
            .OrderBy(t => t.Status == "active" ? 0 : 1)
            .ThenByDescending(t => t.CreatedAt)
            .Skip(Pagination.Skip(pageNumber, pageSize))
            .Take(pageSize)
            .ToListAsync();
        var totalCount = await filtered.CountAsync();
        // Badge counts use the date range only, never the status filter, so they stay put when the tab changes.
        var activeCount = await inRange.CountAsync(t => t.Status == "active");
        var completeCount = await inRange.CountAsync(t => t.Status == "complete");

        return new TaskListResult(
            tasks, totalCount, activeCount, completeCount, Pagination.TotalPages(totalCount, pageSize), pageNumber, pageSize);
    }

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
