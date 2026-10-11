using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Interfaces;
using TaskTrack.Service.Results;

namespace TaskTrack.API.Controllers;

[ApiController]
[Route("api/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _service;

    public DepartmentsController(IDepartmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<DepartmentListItemDto>>> GetActive()
    {
        var departments = await _service.GetActiveAsync();
        return Ok(departments);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DepartmentDetailDto>> GetById(int id)
    {
        var department = await _service.GetByIdAsync(id);

        if (department is null)
            return NotFound();

        return Ok(department);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<DepartmentListItemDto>>> Search(
    [FromQuery] string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError("name", "Name is required.");
            return ValidationProblem(ModelState);
        }

        var departments = await _service.SearchByNameAsync(name.Trim());
        return Ok(departments);
    }

    [Authorize(Roles = "0,1")]
    [HttpPost]
    public async Task<ActionResult<DepartmentDetailDto>> Create(
    [FromBody] CreateDepartmentDto input)
    {
        var created = await _service.CreateAsync(input);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.DepartmentId },
            created);
    }

    [Authorize(Roles = "0,1")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
    int id, [FromBody] UpdateDepartmentDto input)
    {
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

        if (result == DeleteDepartmentResult.NotFound)
            return NotFound();

        if (result == DeleteDepartmentResult.HasProjects)
            return BadRequest(new { message = "Department has linked projects." });

        return NoContent();
    }
}