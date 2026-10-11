using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Interfaces;

namespace TaskTrack.API.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _service;

    public TasksController(ITaskService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskListItemDto>>> GetActive()
    {
        var tasks = await _service.GetActiveAsync();
        return Ok(tasks);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskDetailDto>> GetById(int id)
    {
        var task = await _service.GetByIdAsync(id);

        if (task is null)
            return NotFound();

        return Ok(task);
    }

    [HttpGet("project/{projectId:int}")]
    public async Task<ActionResult<List<TaskListItemDto>>> GetByProject(
    int projectId)
    {
        var tasks = await _service.GetByProjectAsync(projectId);
        return Ok(tasks);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<TaskListItemDto>>> Search(
    [FromQuery] string? title,
    [FromQuery] short? status,
    [FromQuery] short? priority,
    [FromQuery] int? projectId,
    [FromQuery] int? tagId)
    {
        if (status.HasValue && (status.Value < 0 || status.Value > 3))
            ModelState.AddModelError("status", "Status must be 0–3.");

        if (priority.HasValue && (priority.Value < 0 || priority.Value > 3))
            ModelState.AddModelError("priority", "Priority must be 0–3.");

        if (projectId.HasValue && projectId.Value <= 0)
            ModelState.AddModelError("projectId", "ProjectId must be positive.");

        if (tagId.HasValue && tagId.Value <= 0)
            ModelState.AddModelError("tagId", "TagId must be positive.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var tasks = await _service.SearchAsync(
            title, status, priority, projectId, tagId);

        return Ok(tasks);
    }

    [Authorize(Roles = "0,1")]
    [HttpPost]
    public async Task<ActionResult<TaskDetailDto>> Create(
    [FromBody] CreateTaskDto input)
    {
        if (!int.TryParse(User.FindFirst("AccountID")?.Value,
        out var createdById) || createdById <= 0)
        {
            return Unauthorized();
        }
        if (input.ProjectId.HasValue &&
            !await _service.ProjectExistsAsync(input.ProjectId.Value))
        {
            ModelState.AddModelError("ProjectId", "Project does not exist.");
        }

        if (input.TagIds is { Count: > 0 } &&
            !await _service.TagsExistAsync(input.TagIds))
        {
            ModelState.AddModelError(
                "TagIds", "One or more tags do not exist.");
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var created = await _service.CreateAsync(input, createdById);
        return CreatedAtAction(
            nameof(GetById),
            new { id = created.TaskId },
            created);
    }

    [Authorize(Roles = "0,1")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
    int id, [FromBody] UpdateTaskDto input)
    {
        if (input.ProjectId.HasValue &&
            !await _service.ProjectExistsAsync(input.ProjectId.Value))
        {
            ModelState.AddModelError("ProjectId", "Project does not exist.");
        }

        if (input.TagIds is { Count: > 0 } &&
            !await _service.TagsExistAsync(input.TagIds))
        {
            ModelState.AddModelError(
                "TagIds", "One or more tags do not exist.");
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var updated = await _service.UpdateAsync(id, input);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    [Authorize(Roles = "0,1")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.SoftDeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}