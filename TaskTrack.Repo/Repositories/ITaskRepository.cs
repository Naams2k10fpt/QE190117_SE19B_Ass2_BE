using System.Collections.Generic;
using System.Threading.Tasks;
using TaskEntity = TaskTrack.Repo.Models.Task;

namespace TaskTrack.Repo.Repositories;

public interface ITaskRepository
{
    Task<List<TaskEntity>> GetActiveWithProjectAsync();
    Task<TaskEntity?> GetByIdWithDetailsAsync(int id);
    Task<List<TaskEntity>> GetByProjectAsync(int projectId);
    Task<List<TaskEntity>> SearchAsync(
    string? title, short? status, short? priority,
    int? projectId, int? tagId);
    Task<bool> ProjectExistsAsync(int projectId);
    Task<bool> TagsExistAsync(List<int> tagIds);
    Task<TaskEntity> AddAsync(TaskEntity task, List<int> tagIds);
    Task<bool> UpdateAsync(int id, TaskEntity changes, List<int> tagIds);
    Task<bool> SoftDeleteAsync(int id, int updatedById);
}
