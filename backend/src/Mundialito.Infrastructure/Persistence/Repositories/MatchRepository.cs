using Microsoft.EntityFrameworkCore;
using Mundialito.Application.Matches;
using Mundialito.Domain.Matches;

namespace Mundialito.Infrastructure.Persistence.Repositories;

internal sealed class MatchRepository(MundialitoDbContext dbContext) : IMatchRepository
{
    public Task<Match?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken) =>
        dbContext.Matches
            .Include(m => m.Goals)
            .FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken);

    public Task<bool> ExistsForTeamAsync(Guid teamId, CancellationToken cancellationToken) =>
        dbContext.Matches.AnyAsync(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId, cancellationToken);

    public void Add(Match match) => dbContext.Matches.Add(match);
}
