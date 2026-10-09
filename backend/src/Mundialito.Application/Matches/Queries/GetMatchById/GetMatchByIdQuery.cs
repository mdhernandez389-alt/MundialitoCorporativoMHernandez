using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Matches.Queries.GetMatchById;

/// <summary>GET /matches/{id}: el partido con sus goles.</summary>
public sealed record GetMatchByIdQuery(Guid MatchId) : IQuery<MatchDetailDto>;
