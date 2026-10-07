using Microsoft.EntityFrameworkCore;
using Mundialito.Application.Players;
using Mundialito.Domain.Players;

namespace Mundialito.Infrastructure.Persistence.Repositories;

internal sealed class PlayerRepository(MundialitoDbContext dbContext) : IPlayerRepository
{
    public async Task<IReadOnlyList<Player>> GetByIdsAsync(
        IReadOnlyCollection<Guid> playerIds,
        CancellationToken cancellationToken) =>
        await dbContext.Players
            .Where(p => playerIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

    public Task<bool> ShirtNumberTakenAsync(Guid teamId, int shirtNumber, CancellationToken cancellationToken) =>
        dbContext.Players.AnyAsync(p => p.TeamId == teamId && p.ShirtNumber == shirtNumber, cancellationToken);

    public void Add(Player player) => dbContext.Players.Add(player);
}
