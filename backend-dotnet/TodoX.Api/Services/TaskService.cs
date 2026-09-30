using TodoX.Api.Data;
using TodoX.Api.Entities;

namespace TodoX.Api.Services;

public class TaskService(AppDbContext db, TimeProvider timeProvider) : ITaskService
{
    public Task<TaskEntity> CreateAsync(string? title) => throw new NotImplementedException();

    public Task<TaskEntity?> UpdateAsync(string id, TaskUpdate update) => throw new NotImplementedException();

    public Task<TaskEntity?> DeleteAsync(string id) => throw new NotImplementedException();
}
