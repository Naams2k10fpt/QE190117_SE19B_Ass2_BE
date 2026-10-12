using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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

    public Task<List<SystemAccount>> GetAllAsync() => _context.SystemAccounts
        .AsNoTracking().OrderBy(a => a.AccountId).ToListAsync();

    public Task<SystemAccount?> GetByIdAsync(int id) => _context.SystemAccounts
        .AsNoTracking().FirstOrDefaultAsync(a => a.AccountId == id);

    public async Task<bool> UpdateAsync(int id, string? fullName, short? role)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var account = await _context.SystemAccounts.FromSqlInterpolated(
            $"SELECT * FROM public.\"SystemAccount\" WHERE \"AccountID\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync();
        if (account is null)
            return false;
        if (fullName is not null)
            account.FullName = fullName;
        if (role.HasValue && role.Value != account.Role)
        {
            account.Role = role.Value;
            var now = DateTime.UtcNow;
            await _context.RefreshTokens.Where(t => t.AccountId == id && t.RevokedDate == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedDate, (DateTime?)now));
        }
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public Task<bool> HasCreatedTasksAsync(int id) => _context.Tasks.AnyAsync(t => t.CreatedById == id);

    public async Task<bool> DeleteAsync(int id)
    {
        var account = await _context.SystemAccounts.FindAsync(id);
        if (account is null)
            return false;
        _context.SystemAccounts.Remove(account);
        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState == PostgresErrorCodes.ForeignKeyViolation && pg.ConstraintName == "FK_Task_SystemAccount")
        {
            _context.Entry(account).State = EntityState.Detached;
            return false;
        }
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
