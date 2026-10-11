using TaskTrack.Service.DTOs;

namespace TaskTrack.Service.Interfaces;

public interface ITaskService
{
    Task<List<TaskListItemDto>> GetActiveAsync();
    Task<TaskDetailDto?> GetByIdAsync(int id);
    Task<List<TaskListItemDto>> GetByProjectAsync(int projectId);
    Task<List<TaskListItemDto>> SearchAsync(
    string? title, short? status, short? priority,
    int? projectId, int? tagId);
    Task<bool> ProjectExistsAsync(int projectId);
    Task<bool> TagsExistAsync(List<int> tagIds);
    Task<TaskDetailDto> CreateAsync(CreateTaskDto input, int createdById);
    Task<bool> UpdateAsync(int id, UpdateTaskDto input);
    Task<bool> SoftDeleteAsync(int id);
}