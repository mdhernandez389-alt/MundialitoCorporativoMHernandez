namespace Mundialito.Domain.Matches;

/// <summary>
/// Un gol a registrar: quién lo anotó y para qué equipo juega (el handler lo obtiene del jugador).
/// Un jugador con dos goles aparece dos veces. En un autogol, <see cref="TeamId"/> sigue siendo el equipo
/// del jugador: a qué equipo suma el gol lo decide <see cref="Match"/>, que sabe quién es el rival.
/// </summary>
public sealed record GoalScorer(Guid PlayerId, Guid TeamId, bool IsOwnGoal = false);
