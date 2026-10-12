using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Interfaces;
using TaskTrack.Service.Results;

namespace TaskTrack.API.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize(Roles = "1")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _service;

    public AccountsController(IAccountService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AccountDto>> GetById(int id)
    {
        var account = await _service.GetByIdAsync(id);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAccountDto input) =>
        await _service.UpdateAsync(id, input) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) => (await _service.DeleteAsync(id)) switch
    {
        DeleteAccountResult.Deleted => NoContent(),
        DeleteAccountResult.HasTasks => Conflict(new { message = "Account has created tasks, including deleted tasks." }),
        _ => NotFound()
    };
}
