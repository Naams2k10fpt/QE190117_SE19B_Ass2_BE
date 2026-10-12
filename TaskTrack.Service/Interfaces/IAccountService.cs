using TaskTrack.Service.DTOs;
using TaskTrack.Service.Results;

namespace TaskTrack.Service.Interfaces;

public interface IAccountService
{
    Task<List<AccountDto>> GetAllAsync();
    Task<AccountDto?> GetByIdAsync(int id);
    Task<bool> UpdateAsync(int id, UpdateAccountDto input);
    Task<DeleteAccountResult> DeleteAsync(int id);
}
