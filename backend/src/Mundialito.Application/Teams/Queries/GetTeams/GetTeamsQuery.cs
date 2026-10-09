using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;

namespace Mundialito.Application.Teams.Queries.GetTeams;

/// <summary>GET /teams. <c>sortBy</c>: <c>name</c> (por defecto), <c>department</c>.</summary>
public sealed record GetTeamsQuery : PagedQuery, IQuery<PagedResult<TeamDto>>
{
    /// <summary>Filtro "contiene", sin distinguir mayúsculas.</summary>
    public string? Name { get; init; }

    /// <summary>Filtro "contiene", sin distinguir mayúsculas.</summary>
    public string? Department { get; init; }
}
