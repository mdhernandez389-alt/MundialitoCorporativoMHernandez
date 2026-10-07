using Mundialito.Domain.Players;

namespace Mundialito.Application.Players;

/// <summary>
/// Acceso de escritura a los jugadores. Solo rastrea cambios: guardar es trabajo exclusivo del Unit of Work.
/// </summary>
public interface IPlayerRepository
{
    /// <summary>
    /// Trae varios jugadores en una sola consulta (los goleadores de un resultado), sin N+1.
    /// Los Ids que no existen simplemente no vienen: el handler compara para detectarlos.
    /// </summary>
    Task<IReadOnlyList<Player>> GetByIdsAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken);

    Task<bool> ShirtNumberTakenAsync(Guid teamId, int shirtNumber, CancellationToken cancellationToken);

    void Add(Player player);
}
