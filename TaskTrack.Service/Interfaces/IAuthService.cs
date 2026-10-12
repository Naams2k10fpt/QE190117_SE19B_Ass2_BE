using TaskTrack.Service.DTOs;

namespace TaskTrack.Service.Interfaces;

public interface IAuthService
{
    Task<AccountDto?> RegisterAsync(RegisterDto input);
    Task<TokenResponseDto?> LoginAsync(LoginDto input);
    Task<TokenResponseDto?> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<bool> ChangePasswordAsync(int accountId, ChangePasswordDto input);
}
