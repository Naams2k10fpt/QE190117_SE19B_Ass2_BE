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

    public AuthService(
        IAccountRepository repository,
        IPasswordHasher<SystemAccount> passwordHasher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
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
}