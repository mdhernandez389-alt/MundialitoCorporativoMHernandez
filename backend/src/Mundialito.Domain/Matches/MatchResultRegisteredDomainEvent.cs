using Mundialito.Domain.Abstractions;

namespace Mundialito.Domain.Matches;

/// <summary>
/// Se registró (o corrigió) el resultado de un partido.
/// </summary>
/// <param name="PreviousScore">
/// Marcador anterior si fue una corrección; <c>null</c> si es el primer registro.
/// Con él la tabla de posiciones revierte el resultado viejo antes de aplicar el nuevo.
/// </param>
public sealed record MatchResultRegisteredDomainEvent(
    Guid MatchId,
    Guid HomeTeamId,
    Guid AwayTeamId,
    Score Score,
    Score? PreviousScore) : DomainEvent;
