using Mundialito.Domain.Abstractions;

namespace Mundialito.Domain.Matches;

/// <summary>
/// Gol de un partido. Solo existe dentro de <see cref="Match"/> (lo crea y reemplaza el propio partido).
/// </summary>
public sealed class Goal : Entity
{
    private Goal(Guid id, Guid matchId, Guid playerId, Guid teamId)
        : base(id)
    {
        MatchId = matchId;
        PlayerId = playerId;
        TeamId = teamId;
    }

    public Guid MatchId { get; }

    public Guid PlayerId { get; }

    /// <summary>Equipo al que suma el gol (sin autogoles: siempre el equipo del jugador).</summary>
    public Guid TeamId { get; }

    internal static Goal Create(Guid matchId, GoalScorer scorer) =>
        new(Guid.CreateVersion7(), matchId, scorer.PlayerId, scorer.TeamId);
}
