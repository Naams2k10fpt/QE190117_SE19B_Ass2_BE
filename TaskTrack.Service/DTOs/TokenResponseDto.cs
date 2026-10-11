namespace TaskTrack.Service.DTOs;

public sealed record TokenResponseDto(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    AccountDto Account);