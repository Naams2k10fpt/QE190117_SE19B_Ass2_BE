using System.Security.Cryptography;
using System.Text;
using RefreshToken = TaskTrack.Repo.Models.RefreshToken;
using Microsoft.AspNetCore.Identity;
using TaskTrack.Repo.Repositories;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Interfaces;
using SystemAccount = TaskTrack.Repo.Models.SystemAccount;

namespace TaskTrack.Service.Services;

public class AuthService : IAuthService
{
    private readonly IAccountRepository _repository;
    private readonly IPasswordHasher<SystemAccount> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public AuthService(
        IAccountRepository repository,
        IPasswordHasher<SystemAccount> passwordHasher,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<AccountDto?> RegisterAsync(RegisterDto input)
    {
        var email = input.Email.Trim().ToLowerInvariant();

        if (await _repository.GetByEmailAsync(email) is not null)
            return null;

        var account = new SystemAccount
        {
            FullName = input.FullName.Trim(),
            Email = email,
            Role = 0
        };

        account.PasswordHash = _passwordHasher.HashPassword(
            account, input.Password);

        if (!await _repository.TryAddAsync(account))
            return null;

        return new AccountDto(
            account.AccountId,
            account.FullName,
            account.Email,
            account.Role,
            account.CreatedDate);
    }
    public async Task<TokenResponseDto?> LoginAsync(LoginDto input)
    {
        var account = await _repository.GetByEmailAsync(input.Email);

        if (account is null)
            return null;

        var verification = _passwordHasher.VerifyHashedPassword(
            account, account.PasswordHash, input.Password);

        if (verification == PasswordVerificationResult.Failed)
            return null;

        var now = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();

        var accessTokenExpiresAt = now.AddMinutes(15);
        var refreshTokenExpiresAt = now.AddDays(7);

        var refreshToken = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));

        var refreshTokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)))
            .ToLowerInvariant();

        var accessToken = _tokenService.CreateAccessToken(
            account, sessionId, accessTokenExpiresAt);

        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            AccountId = account.AccountId,
            SessionId = sessionId,
            TokenHash = refreshTokenHash,
            CreatedDate = now,
            ExpiresDate = refreshTokenExpiresAt
        });

        return new TokenResponseDto(
            accessToken,
            accessTokenExpiresAt,
            refreshToken,
            refreshTokenExpiresAt,
            new AccountDto(
                account.AccountId,
                account.FullName,
                account.Email,
                account.Role,
                account.CreatedDate));
    }
}