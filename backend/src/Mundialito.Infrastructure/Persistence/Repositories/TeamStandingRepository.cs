using Microsoft.EntityFrameworkCore;
using Mundialito.Application.Standings;
using Mundialito.Domain.Standings;

namespace Mundialito.Infrastructure.Persistence.Repositories;

internal sealed class TeamStandingRepository(MundialitoDbContext dbContext) : ITeamStandingRepository
{
    public Task<TeamStanding?> GetByTeamIdAsync(Guid teamId, CancellationToken cancellationToken) =>
        dbContext.TeamStandings.FirstOrDefaultAsync(s => s.TeamId == teamId, cancellationToken);

    public void Add(TeamStanding standing) => dbContext.TeamStandings.Add(standing);
}
