using TaskTrack.Repo.Repositories;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Interfaces;

namespace TaskTrack.Service.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _repository;

    public TaskService(ITaskRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<TaskListItemDto>> GetActiveAsync()
    {
        var tasks = await _repository.GetActiveWithProjectAsync();

        return tasks
            .Select(t => new TaskListItemDto(
                t.TaskId,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                t.DueDate,
                t.ProjectId,
                t.Project.ProjectName,
                t.CreatedDate,
                t.ModifiedDate))
            .ToList();
    }
    public async Task<TaskDetailDto?> GetByIdAsync(int id)
    {
        var task = await _repository.GetByIdWithDetailsAsync(id);

        if (task is null)
            return null;

        var tags = task.Tags
            .OrderBy(tag => tag.TagName)
            .Select(tag => new TaskTagDto(
                tag.TagId, tag.TagName, tag.Color))
            .ToList();

        return new TaskDetailDto(
            task.TaskId,
            task.Title,
            task.Description,
            task.Status,
            task.Priority,
            task.DueDate,
            task.ProjectId,
            task.Project.ProjectName,
            task.IsActive,
            task.CreatedDate,
            task.ModifiedDate,
            tags);
    }
    public async Task<List<TaskListItemDto>> GetByProjectAsync(int projectId)
    {
        var tasks = await _repository.GetByProjectAsync(projectId);

        return tasks
            .Select(t => new TaskListItemDto(
                t.TaskId,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                t.DueDate,
                t.ProjectId,
                t.Project.ProjectName,
                t.CreatedDate,
                t.ModifiedDate))
            .ToList();
    }
    public async Task<List<TaskListItemDto>> SearchAsync(
    string? title, short? status, short? priority,
    int? projectId, int? tagId)
    {
        var tasks = await _repository.SearchAsync(
            title, status, priority, projectId, tagId);

        return tasks
            .Select(t => new TaskListItemDto(
                t.TaskId,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                t.DueDate,
                t.ProjectId,
                t.Project.ProjectName,
                t.CreatedDate,
                t.ModifiedDate))
            .ToList();
    }
    public Task<bool> ProjectExistsAsync(int projectId)
    {
        return _repository.ProjectExistsAsync(projectId);
    }

    public Task<bool> TagsExistAsync(List<int> tagIds)
    {
        return _repository.TagsExistAsync(tagIds);
    }

    public async Task<TaskDetailDto> CreateAsync(CreateTaskDto input, int createdById)
    {
        var task = new TaskTrack.Repo.Models.Task
        {
            Title = input.Title.Trim(),
            Description = input.Description?.Trim(),
            Status = input.Status!.Value,
            Priority = input.Priority!.Value,
            DueDate = input.DueDate,
            ProjectId = input.ProjectId!.Value,
            CreatedById = createdById,
            IsActive = true
        };

        var saved = await _repository.AddAsync(
            task, input.TagIds ?? new List<int>());

        return await GetByIdAsync(saved.TaskId)
            ?? throw new InvalidOperationException(
                "Created task could not be loaded.");
    }
    public Task<bool> UpdateAsync(int id, UpdateTaskDto input)
    {
        var changes = new TaskTrack.Repo.Models.Task
        {
            Title = input.Title.Trim(),
            Description = input.Description?.Trim(),
            Status = input.Status!.Value,
            Priority = input.Priority!.Value,
            DueDate = input.DueDate,
            ProjectId = input.ProjectId!.Value
        };

        return _repository.UpdateAsync(id, changes, input.TagIds!);
    }
    public Task<bool> SoftDeleteAsync(int id)
    {
        return _repository.SoftDeleteAsync(id);
    }
}