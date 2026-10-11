using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Interfaces;
using TaskTrack.Service.Results;
using Microsoft.AspNetCore.Authorization;

namespace TaskTrack.API.Controllers;

[ApiController]
[Route("api/tags")]
public class TagsController : ControllerBase
{
    private readonly ITagService _service;

    public TagsController(ITagService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskTagDto>>> GetAll()
    {
        var tags = await _service.GetAllAsync();
        return Ok(tags);
    }

    [Authorize(Roles = "0,1")]
    [HttpPost]
    public async Task<ActionResult<TaskTagDto>> Create(
    [FromBody] CreateTagDto input)
    {
        if (await _service.NameExistsAsync(input.TagName))
        {
            ModelState.AddModelError(
                "TagName", "Tag name already exists.");
            return ValidationProblem(ModelState);
        }

        var created = await _service.CreateAsync(input);
        return StatusCode(201, created);
    }

    [Authorize(Roles = "0,1")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
    int id, [FromBody] CreateTagDto input)
    {
        if (await _service.NameExistsAsync(input.TagName, id))
        {
            ModelState.AddModelError(
                "TagName", "Tag name already exists.");
            return ValidationProblem(ModelState);
        }

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

        if (result == DeleteTagResult.NotFound)
            return NotFound();

        if (result == DeleteTagResult.HasTasks)
            return BadRequest(new { message = "Tag has linked tasks." });

        return NoContent();
    }
}