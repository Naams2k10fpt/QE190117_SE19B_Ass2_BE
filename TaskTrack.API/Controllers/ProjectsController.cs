using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Interfaces;
using TaskTrack.Service.Results;

namespace TaskTrack.API.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _service;

    public ProjectsController(IProjectService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProjectListItemDto>>> GetActive()
    {
        var projects = await _service.GetActiveAsync();
        return Ok(projects);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDetailDto>> GetById(int id)
    {
        var project = await _service.GetByIdAsync(id);

        if (project is null)
            return NotFound();

        return Ok(project);
    }

    [HttpGet("department/{departmentId:int}")]
    public async Task<ActionResult<List<ProjectListItemDto>>> GetByDepartment(
    int departmentId)
    {
        var projects = await _service.GetByDepartmentAsync(departmentId);
        return Ok(projects);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<ProjectListItemDto>>> Search(
    [FromQuery] string? name,
    [FromQuery] short? status,
    [FromQuery] int? departmentId)
    {
        if (status.HasValue && (status.Value < 0 || status.Value > 3))
            ModelState.AddModelError("status", "Status must be 0–3.");

        if (departmentId.HasValue && departmentId.Value <= 0)
            ModelState.AddModelError(
                "departmentId", "DepartmentId must be positive.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var projects = await _service.SearchAsync(
            name, status, departmentId);

        return Ok(projects);
    }

    [Authorize(Roles = "0,1")]
    [HttpPost]
    public async Task<ActionResult<ProjectDetailDto>> Create(
    [FromBody] CreateProjectDto input)
    {
        if (input.StartDate.HasValue && input.EndDate.HasValue &&
            input.EndDate.Value < input.StartDate.Value)
        {
            ModelState.AddModelError(
                "EndDate", "EndDate cannot be before StartDate.");
        }

        if (input.DepartmentId.HasValue &&
            !await _service.DepartmentExistsAsync(input.DepartmentId.Value))
        {
            ModelState.AddModelError(
                "DepartmentId", "Department does not exist.");
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var created = await _service.CreateAsync(input);
        return CreatedAtAction(
            nameof(GetById),
            new { id = created.ProjectId },
            created);
    }

    [Authorize(Roles = "0,1")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
    int id, [FromBody] UpdateProjectDto input)
    {
        if (input.StartDate.HasValue && input.EndDate.HasValue &&
            input.EndDate.Value < input.StartDate.Value)
        {
            ModelState.AddModelError(
                "EndDate", "EndDate cannot be before StartDate.");
        }

        if (input.DepartmentId.HasValue &&
            !await _service.DepartmentExistsAsync(input.DepartmentId.Value))
        {
            ModelState.AddModelError(
                "DepartmentId", "Department does not exist.");
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
        var result = await _service.DeleteAsync(id);

        if (result == DeleteProjectResult.NotFound)
            return NotFound();

        if (result == DeleteProjectResult.HasTasks)
            return BadRequest(new { message = "Project has linked tasks." });

        return NoContent();
    }
}