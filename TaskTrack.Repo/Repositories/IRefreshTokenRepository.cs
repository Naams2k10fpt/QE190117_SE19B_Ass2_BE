using System.Threading.Tasks;
using RefreshToken = TaskTrack.Repo.Models.RefreshToken;
using System;

namespace TaskTrack.Repo.Repositories;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token);
    Task<bool> HasActiveSessionAsync(int accountId, Guid sessionId, DateTime now);
    Task<RefreshToken?> RotateAsync(string tokenHash, string nextTokenHash, DateTime now);
    Task RevokeSessionAsync(string tokenHash, DateTime now);
}
