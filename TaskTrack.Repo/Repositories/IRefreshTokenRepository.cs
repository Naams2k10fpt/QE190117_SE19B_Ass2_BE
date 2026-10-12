using System.Threading.Tasks;
using RefreshToken = TaskTrack.Repo.Models.RefreshToken;
using System;

namespace TaskTrack.Repo.Repositories;

public interface IRefreshTokenRepository
{
    Task<TaskTrack.Repo.Models.SystemAccount?> StartSessionAsync(RefreshToken token, string expectedPasswordHash);
    Task<bool> HasActiveSessionAsync(int accountId, Guid sessionId, DateTime now);
    Task<RefreshToken?> RotateAsync(string tokenHash, string nextTokenHash, DateTime now);
    Task RevokeSessionAsync(string tokenHash, DateTime now);
}
