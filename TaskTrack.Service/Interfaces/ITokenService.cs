using TaskTrack.Repo.Models;

namespace TaskTrack.Service.Interfaces;

public interface ITokenService
{
    string CreateAccessToken(
        SystemAccount account,
        Guid sessionId,
        DateTime expiresAt);
}