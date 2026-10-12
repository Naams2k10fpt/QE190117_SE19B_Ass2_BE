using TaskTrack.Repo.Repositories;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Interfaces;
using TaskTrack.Service.Results;
using TaskTrack.Repo.Models;

namespace TaskTrack.Service.Services;

public class AccountService : IAccountService
{
    private readonly IAccountRepository _repository;

    public AccountService(IAccountRepository repository) => _repository = repository;

    private static AccountDto ToDto(SystemAccount a) =>
        new(a.AccountId, a.FullName, a.Email, a.Role, a.CreatedDate);

    public async Task<List<AccountDto>> GetAllAsync() =>
        (await _repository.GetAllAsync()).Select(ToDto).ToList();

    public async Task<AccountDto?> GetByIdAsync(int id)
    {
        var account = await _repository.GetByIdAsync(id);
        return account is null ? null : ToDto(account);
    }

    public Task<bool> UpdateAsync(int id, UpdateAccountDto input) =>
        _repository.UpdateAsync(id, input.FullName?.Trim(), input.Role);

    public async Task<DeleteAccountResult> DeleteAsync(int id)
    {
        if (await _repository.GetByIdAsync(id) is null)
            return DeleteAccountResult.NotFound;
        if (await _repository.HasCreatedTasksAsync(id))
            return DeleteAccountResult.HasTasks;
        if (await _repository.DeleteAsync(id))
            return DeleteAccountResult.Deleted;
        return await _repository.HasCreatedTasksAsync(id)
            ? DeleteAccountResult.HasTasks : DeleteAccountResult.NotFound;
    }
}
