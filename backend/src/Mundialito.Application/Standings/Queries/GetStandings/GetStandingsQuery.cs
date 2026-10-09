using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;

namespace Mundialito.Application.Standings.Queries.GetStandings;

/// <summary>
/// GET /standings. <c>sortBy</c>: <c>position</c> (por defecto, el orden oficial del §8), <c>points</c>,
/// <c>goalDifference</c>, <c>goalsFor</c>, <c>teamName</c>.
/// </summary>
public sealed record GetStandingsQuery : PagedQuery, IQuery<PagedResult<StandingDto>>
{
    /// <summary>Filtro "contiene", sin distinguir mayúsculas.</summary>
    public string? TeamName { get; init; }

    /// <summary>Filtro "contiene", sin distinguir mayúsculas.</summary>
    public string? Department { get; init; }
}
