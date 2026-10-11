using System.Threading.Tasks;
using SystemAccount = TaskTrack.Repo.Models.SystemAccount;

namespace TaskTrack.Repo.Repositories;

public interface IAccountRepository
{
    Task<SystemAccount?> GetByEmailAsync(string email);

    Task<bool> TryAddAsync(SystemAccount account);
}