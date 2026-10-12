using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskTrack.Repo.Data;
using TaskEntity = TaskTrack.Repo.Models.Task;

namespace TaskTrack.Repo.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly TaskTrackDbContext _context;

    public TaskRepository(TaskTrackDbContext context)
    {
        _context = context;
    }

    public Task<List<TaskEntity>> GetActiveWithProjectAsync()
    {
        return _context.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .Where(t => t.IsActive)
            .OrderBy(t => t.TaskId)
            .ToListAsync();
    }
    public Task<TaskEntity?> GetByIdWithDetailsAsync(int id)
    {
        return _context.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.Tags)
            .FirstOrDefaultAsync(t => t.TaskId == id && t.IsActive);
    }
    public Task<List<TaskEntity>> GetByProjectAsync(int projectId)
    {
        return _context.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .Where(t => t.IsActive && t.ProjectId == projectId)
            .OrderBy(t => t.TaskId)
            .ToListAsync();
    }
    public Task<List<TaskEntity>> SearchAsync(
    string? title, short? status, short? priority,
    int? projectId, int? tagId)
    {
        IQueryable<TaskEntity> query = _context.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(title))
        {
            var pattern = $"%{title.Trim()}%";
            query = query.Where(t => EF.Functions.ILike(t.Title, pattern));
        }

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (priority.HasValue)
            query = query.Where(t => t.Priority == priority.Value);

        if (projectId.HasValue)
            query = query.Where(t => t.ProjectId == projectId.Value);

        if (tagId.HasValue)
            query = query.Where(t =>
                t.Tags.Any(tag => tag.TagId == tagId.Value));

        return query
            .OrderBy(t => t.TaskId)
            .ToListAsync();
    }
    public Task<bool> ProjectExistsAsync(int projectId)
    {
        return _context.Projects
            .AnyAsync(p => p.ProjectId == projectId);
    }

    public async Task<bool> TagsExistAsync(List<int> tagIds)
    {
        var ids = tagIds.Distinct().ToArray();

        if (ids.Length == 0)
            return true;

        var count = await _context.Tags
            .CountAsync(tag => ids.Contains(tag.TagId));

        return count == ids.Length;
    }

    public async Task<TaskEntity> AddAsync(
        TaskEntity task, List<int> tagIds)
    {
        var ids = tagIds.Distinct().ToArray();

        if (ids.Length > 0)
        {
            var tags = await _context.Tags
                .Where(tag => ids.Contains(tag.TagId))
                .ToListAsync();

            foreach (var tag in tags)
                task.Tags.Add(tag);
        }

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }
    public async Task<bool> UpdateAsync(
    int id, TaskEntity changes, List<int> tagIds)
    {
        var task = await _context.Tasks
            .Include(t => t.Tags)
            .FirstOrDefaultAsync(t => t.TaskId == id && t.IsActive);

        if (task is null)
            return false;

        task.Title = changes.Title;
        task.Description = changes.Description;
        task.Status = changes.Status;
        task.Priority = changes.Priority;
        task.DueDate = changes.DueDate;
        task.ProjectId = changes.ProjectId;
        task.UpdatedById = changes.UpdatedById;
        task.ModifiedDate = DateTime.SpecifyKind(
            DateTime.UtcNow, DateTimeKind.Unspecified);

        var requestedIds = tagIds.ToHashSet();

        foreach (var tag in task.Tags
            .Where(t => !requestedIds.Contains(t.TagId))
            .ToList())
        {
            task.Tags.Remove(tag);
        }

        var currentIds = task.Tags.Select(t => t.TagId).ToHashSet();
        var requestedTags = await _context.Tags
            .Where(t => requestedIds.Contains(t.TagId))
            .ToListAsync();

        foreach (var tag in requestedTags)
        {
            if (currentIds.Add(tag.TagId))
                task.Tags.Add(tag);
        }

        await _context.SaveChangesAsync();
        return true;
    }
    public async Task<bool> SoftDeleteAsync(int id, int updatedById)
    {
        var task = await _context.Tasks
            .FirstOrDefaultAsync(t => t.TaskId == id && t.IsActive);

        if (task is null)
            return false;

        task.IsActive = false;
        task.UpdatedById = updatedById;
        task.ModifiedDate = DateTime.SpecifyKind(
            DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync();
        return true;
    }
}
