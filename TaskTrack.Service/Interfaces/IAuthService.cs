using TaskTrack.Service.DTOs;

namespace TaskTrack.Service.Interfaces;

public interface IAuthService
{
    Task<AccountDto?> RegisterAsync(RegisterDto input);
    Task<TokenResponseDto?> LoginAsync(LoginDto input);
}