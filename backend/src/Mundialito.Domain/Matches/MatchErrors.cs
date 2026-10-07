using Mundialito.Domain.Results;

namespace Mundialito.Domain.Matches;

public static class MatchErrors
{
    public static readonly Error SameTeams =
        Error.Validation("Match.SameTeams", "Un equipo no puede jugar contra sí mismo.");

    public static readonly Error ScorerNotInMatch =
        Error.Validation("Match.ScorerNotInMatch", "Todos los goleadores deben pertenecer a uno de los dos equipos del partido.");

    public static Error NotFound(Guid matchId) =>
        Error.NotFound("Match.NotFound", $"No existe el partido '{matchId}'.");
}
