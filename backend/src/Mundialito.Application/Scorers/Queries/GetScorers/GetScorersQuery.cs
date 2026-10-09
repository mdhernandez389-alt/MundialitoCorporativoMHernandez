using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;

namespace Mundialito.Application.Scorers.Queries.GetScorers;

/// <summary>
/// GET /scorers. <c>sortBy</c>: <c>position</c> (por defecto, más goles primero), <c>goals</c>,
/// <c>playerName</c>, <c>teamName</c>.
/// </summary>
public sealed record GetScorersQuery : PagedQuery, IQuery<PagedResult<ScorerDto>>
{
    /// <summary>Solo los goleadores de este equipo (su posición sigue siendo la del torneo).</summary>
    public Guid? TeamId { get; init; }

    /// <summary>Filtro "contiene" sobre el nombre, sin distinguir mayúsculas.</summary>
    public string? PlayerName { get; init; }
}
