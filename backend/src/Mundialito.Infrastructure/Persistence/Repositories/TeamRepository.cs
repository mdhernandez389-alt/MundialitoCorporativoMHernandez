using Microsoft.EntityFrameworkCore;
using Mundialito.Application.Teams;
using Mundialito.Domain.Teams;

namespace Mundialito.Infrastructure.Persistence.Repositories;

internal sealed class TeamRepository(MundialitoDbContext dbContext) : ITeamRepository
{
    public Task<Team?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken) =>
        dbContext.Teams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);

    public Task<bool> ExistsAsync(Guid teamId, CancellationToken cancellationToken) =>
        dbContext.Teams.AnyAsync(t => t.Id == teamId, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludingTeamId, CancellationToken cancellationToken)
    {
        // La collation por defecto de SQL Server ignora mayúsculas: "Sistemas" y "sistemas" son el mismo nombre,
        // igual que para el índice único.
        var trimmedName = name.Trim();

        return dbContext.Teams.AnyAsync(
            t => t.Name == trimmedName && t.Id != excludingTeamId,
            cancellationToken);
    }

    public void Add(Team team) => dbContext.Teams.Add(team);

    public void Remove(Team team) => dbContext.Teams.Remove(team);
}
