using System.Threading.Tasks;
using RefreshToken = TaskTrack.Repo.Models.RefreshToken;

namespace TaskTrack.Repo.Repositories;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token);
}