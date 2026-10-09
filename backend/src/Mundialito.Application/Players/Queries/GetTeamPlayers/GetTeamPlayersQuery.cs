using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;

namespace Mundialito.Application.Players.Queries.GetTeamPlayers;

/// <summary>
/// GET /teams/{teamId}/players. <c>sortBy</c>: <c>shirtNumber</c> (por defecto), <c>fullName</c>.
/// </summary>
public sealed record GetTeamPlayersQuery : PagedQuery, IQuery<PagedResult<PlayerDto>>
{
    /// <summary>Viene de la ruta, no del query string.</summary>
    public Guid TeamId { get; init; }

    /// <summary>Filtro "contiene" sobre el nombre, sin distinguir mayúsculas.</summary>
    public string? Name { get; init; }
}
