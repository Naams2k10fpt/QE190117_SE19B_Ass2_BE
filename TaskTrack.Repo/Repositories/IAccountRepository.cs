using System.Threading.Tasks;
using SystemAccount = TaskTrack.Repo.Models.SystemAccount;
using System.Collections.Generic;

namespace TaskTrack.Repo.Repositories;

public interface IAccountRepository
{
    Task<SystemAccount?> GetByEmailAsync(string email);

    Task<bool> TryAddAsync(SystemAccount account);
    Task<List<SystemAccount>> GetAllAsync();
    Task<SystemAccount?> GetByIdAsync(int id);
    Task<bool> UpdateAsync(int id, string? fullName, short? role);
    Task<bool> HasCreatedTasksAsync(int id);
    Task<bool> DeleteAsync(int id);
}
