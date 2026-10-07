namespace Mundialito.Domain.Matches;

/// <summary>
/// Un gol a registrar: quién lo anotó y para qué equipo juega (el handler lo obtiene del jugador).
/// Un jugador con dos goles aparece dos veces.
/// </summary>
public sealed record GoalScorer(Guid PlayerId, Guid TeamId);
