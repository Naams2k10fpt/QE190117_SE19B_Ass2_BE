using System.Threading.Tasks;
using TaskTrack.Repo.Data;
using RefreshToken = TaskTrack.Repo.Models.RefreshToken;

namespace TaskTrack.Repo.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly TaskTrackDbContext _context;

    public RefreshTokenRepository(TaskTrackDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RefreshToken token)
    {
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();
    }
}