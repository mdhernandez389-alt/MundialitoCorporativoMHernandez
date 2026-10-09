using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;

namespace Mundialito.Application.Matches.Queries.GetMatches;

/// <summary>
/// GET /matches con los filtros del §8 (fecha, equipo, estado).
/// <c>sortBy</c>: <c>scheduledAt</c> (por defecto), <c>status</c>.
/// </summary>
public sealed record GetMatchesQuery : PagedQuery, IQuery<PagedResult<MatchDto>>
{
    /// <summary>Desde esta fecha y hora, inclusive (con zona horaria; se compara en UTC).</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Hasta esta fecha y hora, inclusive.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Partidos donde este equipo juega como local o como visitante.</summary>
    public Guid? TeamId { get; init; }

    /// <summary><c>Scheduled</c> o <c>Played</c> (sin distinguir mayúsculas).</summary>
    public string? Status { get; init; }
}
