using System.Threading.Tasks;
using TaskTrack.Repo.Data;
using System;
using System.Linq;
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

    private Task<int?> GetAccountIdAsync(string hash) => _context.RefreshTokens.AsNoTracking()
        .Where(t => t.TokenHash == hash).Select(t => (int?)t.AccountId).SingleOrDefaultAsync();

    public async Task<RefreshToken?> RotateAsync(string tokenHash, string nextTokenHash, DateTime now)
    {
        var accountId = await GetAccountIdAsync(tokenHash);
        if (accountId is null)
            return null;
        await using var transaction = await _context.Database.BeginTransactionAsync();
        // Lock accounts first everywhere so rotation cannot race role revocation.
        var account = await _context.SystemAccounts.FromSqlInterpolated(
            $"SELECT * FROM public.\"SystemAccount\" WHERE \"AccountID\" = {accountId.Value} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (account is null)
            return null;
        var token = await _context.RefreshTokens.FromSqlInterpolated(
            $"SELECT * FROM public.\"RefreshToken\" WHERE \"TokenHash\" = {tokenHash} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (token is null)
            return null;
        if (token.RevokedDate is not null || token.ExpiresDate <= now)
        {
            await _context.RefreshTokens.Where(t => t.SessionId == token.SessionId && t.RevokedDate == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedDate, (DateTime?)now));
            await transaction.CommitAsync();
            return null;
        }

        token.RevokedDate = now;
        var replacement = new RefreshToken
        {
            AccountId = token.AccountId, Account = account, SessionId = token.SessionId,
            TokenHash = nextTokenHash, CreatedDate = now, ExpiresDate = token.ExpiresDate
        };
        _context.RefreshTokens.Add(replacement);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return replacement;
    }

    public async Task RevokeSessionAsync(string tokenHash, DateTime now)
    {
        var accountId = await GetAccountIdAsync(tokenHash);
        if (accountId is null)
            return;
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var account = await _context.SystemAccounts.FromSqlInterpolated(
            $"SELECT * FROM public.\"SystemAccount\" WHERE \"AccountID\" = {accountId.Value} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (account is null)
            return;
        var sessionId = await _context.RefreshTokens.Where(t => t.TokenHash == tokenHash)
            .Select(t => t.SessionId).SingleOrDefaultAsync();
        await _context.RefreshTokens.Where(t => t.SessionId == sessionId && t.RevokedDate == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedDate, (DateTime?)now));
        await transaction.CommitAsync();
    }

    public async Task<TaskTrack.Repo.Models.SystemAccount?> StartSessionAsync(RefreshToken token, string expectedPasswordHash)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var account = await _context.SystemAccounts.FromSqlInterpolated(
            $"SELECT * FROM public.\"SystemAccount\" WHERE \"AccountID\" = {token.AccountId} FOR UPDATE")
            .SingleOrDefaultAsync();
        // A login verified before a password change cannot create a session afterwards.
        if (account is null || account.PasswordHash != expectedPasswordHash)
            return null;
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return account;
    }
}
