using System.Threading.Tasks;
using TaskTrack.Repo.Data;
using System;
using Microsoft.EntityFrameworkCore;
using RefreshToken = TaskTrack.Repo.Models.RefreshToken;

namespace TaskTrack.Repo.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly TaskTrackDbContext _context;

    public RefreshTokenRepository(TaskTrackDbContext context)
    {
        _context = context;
    }

    public Task<bool> HasActiveSessionAsync(int accountId, Guid sessionId, DateTime now) =>
        _context.RefreshTokens.AnyAsync(t => t.AccountId == accountId && t.SessionId == sessionId &&
            t.RevokedDate == null && t.ExpiresDate > now);

    public async Task AddAsync(RefreshToken token)
    {
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();
    }
}
