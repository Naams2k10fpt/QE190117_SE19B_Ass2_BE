using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Interfaces;

namespace TaskTrack.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _service;
    private readonly IAccountService _accounts;

    public AuthController(IAuthService service, IAccountService accounts)
    {
        _service = service;
        _accounts = accounts;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AccountDto>> Register(
        [FromBody] RegisterDto input)
    {
        var account = await _service.RegisterAsync(input);

        if (account is null)
            return Conflict(new { message = "Email already exists." });

        return StatusCode(StatusCodes.Status201Created, account);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TokenResponseDto>> Login(
    [FromBody] LoginDto input)
    {
        var result = await _service.LoginAsync(input);

        if (result is null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponseDto>> Refresh([FromBody] RefreshTokenDto input)
    {
        var result = await _service.RefreshAsync(input.RefreshToken);
        return result is null ? Unauthorized(new { message = "Invalid or expired refresh token." }) : Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenDto input)
    {
        await _service.LogoutAsync(input.RefreshToken);
        return NoContent();
    }

    [Authorize(Roles = "0,1")]
    [HttpGet("me")]
    public async Task<ActionResult<AccountDto>> Me()
    {
        if (!int.TryParse(User.FindFirst("AccountID")?.Value, out var id))
            return Unauthorized();
        var account = await _accounts.GetByIdAsync(id);
        return account is null ? Unauthorized() : Ok(account);
    }
}
