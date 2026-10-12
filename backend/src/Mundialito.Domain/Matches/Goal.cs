using Mundialito.Domain.Abstractions;

namespace Mundialito.Domain.Matches;

/// <summary>
/// Gol de un partido. Solo existe dentro de <see cref="Match"/> (lo crea y reemplaza el propio partido).
/// </summary>
public sealed class Goal : Entity
{
    private Goal(Guid id, Guid matchId, Guid playerId, Guid teamId, bool isOwnGoal)
        : base(id)
    {
        MatchId = matchId;
        PlayerId = playerId;
        TeamId = teamId;
        IsOwnGoal = isOwnGoal;
    }

    public Guid MatchId { get; }

    public Guid PlayerId { get; }

    /// <summary>
    /// Equipo al que suma el gol: el del jugador o, si es autogol, el rival. El marcador se cuenta con este campo.
    /// </summary>
    public Guid TeamId { get; }

    /// <summary>Un autogol suma al rival y no cuenta en la tabla de goleadores.</summary>
    public bool IsOwnGoal { get; }

    internal static Goal Create(Guid matchId, GoalScorer scorer, Guid creditedTeamId) =>
        new(Guid.CreateVersion7(), matchId, scorer.PlayerId, creditedTeamId, scorer.IsOwnGoal);
}
