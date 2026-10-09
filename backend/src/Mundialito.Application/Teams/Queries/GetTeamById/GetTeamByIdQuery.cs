using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Teams.Queries.GetTeamById;

/// <summary>GET /teams/{id}: un recurso, no una lista (no se pagina).</summary>
public sealed record GetTeamByIdQuery(Guid TeamId) : IQuery<TeamDto>;
