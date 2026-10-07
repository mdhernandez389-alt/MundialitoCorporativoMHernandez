using Mundialito.Domain.Matches;

namespace Mundialito.Application.Matches;

/// <summary>
/// Acceso de escritura a los partidos. Solo rastrea cambios: guardar es trabajo exclusivo del Unit of Work.
/// </summary>
public interface IMatchRepository
{
    /// <summary>Trae el agregado completo (partido + goles): sin sus goles no puede registrar el resultado.</summary>
    Task<Match?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken);

    /// <summary>Si el equipo juega (o jugó) algún partido, como local o visitante. Regla para eliminar equipos.</summary>
    Task<bool> ExistsForTeamAsync(Guid teamId, CancellationToken cancellationToken);

    void Add(Match match);
}
