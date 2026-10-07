using Mundialito.Domain.Standings;

namespace Mundialito.Application.Standings;

/// <summary>
/// Acceso de escritura a la tabla de posiciones. Se modifica en la misma transacción que el resultado
/// del partido; la tabla ordenada que ve el usuario la lee Dapper, no este repositorio.
/// </summary>
public interface ITeamStandingRepository
{
    Task<TeamStanding?> GetByTeamIdAsync(Guid teamId, CancellationToken cancellationToken);

    void Add(TeamStanding standing);
}
