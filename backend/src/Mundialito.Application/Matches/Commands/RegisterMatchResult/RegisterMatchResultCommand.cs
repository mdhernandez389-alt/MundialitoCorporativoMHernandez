using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Matches.Commands.RegisterMatchResult;

/// <summary>
/// PUT /matches/{id}/result: registra o corrige el resultado completo (idempotente).
/// </summary>
/// <param name="ScorerPlayerIds">
/// Un Id por gol: un jugador con dos goles aparece dos veces. Lista vacía = 0 a 0.
/// El marcador no se recibe: se deriva de esta lista.
/// </param>
public sealed record RegisterMatchResultCommand(Guid MatchId, IReadOnlyCollection<Guid> ScorerPlayerIds) : ICommand;
