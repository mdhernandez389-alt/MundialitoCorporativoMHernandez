using Mundialito.Domain.Teams;

namespace Mundialito.Application.Teams;

/// <summary>
/// Acceso de escritura a los equipos. Solo rastrea cambios: guardar es trabajo exclusivo del Unit of Work.
/// </summary>
public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Regla de nombre único. <paramref name="excludingTeamId"/> permite que un PUT conserve su propio nombre.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? excludingTeamId, CancellationToken cancellationToken);

    void Add(Team team);

    void Remove(Team team);
}
