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

    public void Add(Match match) => dbContext.Matches.Add(match);
}
