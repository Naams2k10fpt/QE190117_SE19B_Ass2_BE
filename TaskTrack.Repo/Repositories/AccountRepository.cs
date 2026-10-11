using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskTrack.Repo.Data;
using SystemAccount = TaskTrack.Repo.Models.SystemAccount;

namespace TaskTrack.Repo.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly TaskTrackDbContext _context;

    public AccountRepository(TaskTrackDbContext context)
    {
        _context = context;
    }

    public Task<SystemAccount?> GetByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return _context.SystemAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Email == normalizedEmail);
    }

    public async Task<bool> TryAddAsync(SystemAccount account)
    {
        _context.SystemAccounts.Add(account);

        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pgEx &&
            pgEx.SqlState == PostgresErrorCodes.UniqueViolation &&
            pgEx.ConstraintName == "SystemAccount_Email_key")
        {
            _context.Entry(account).State = EntityState.Detached;
            return false;
        }
    }
}