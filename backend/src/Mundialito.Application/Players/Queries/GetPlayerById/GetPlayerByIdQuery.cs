using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Players.Queries.GetPlayerById;

/// <summary>GET /teams/{teamId}/players/{playerId}: el jugador debe pertenecer a ese equipo.</summary>
public sealed record GetPlayerByIdQuery(Guid TeamId, Guid PlayerId) : IQuery<PlayerDto>;
