using Mundialito.Domain.Abstractions;
using Mundialito.Domain.Results;

namespace Mundialito.Domain.Matches;

/// <summary>
/// Partido del calendario. Es el agregado dueño de sus goles: el marcador siempre se deriva de ellos,
/// así la tabla de posiciones y la de goleadores nunca se contradicen.
/// </summary>
public sealed class Match : Entity
{
    private readonly List<Goal> _goals = [];

    private Match(Guid id, Guid homeTeamId, Guid awayTeamId, DateTime scheduledAtUtc)
        : base(id)
    {
        HomeTeamId = homeTeamId;
        AwayTeamId = awayTeamId;
        ScheduledAtUtc = scheduledAtUtc;
        Status = MatchStatus.Scheduled;
    }

    public Guid HomeTeamId { get; }

    public Guid AwayTeamId { get; }

    public DateTime ScheduledAtUtc { get; private set; }

    public MatchStatus Status { get; private set; }

    /// <summary><c>null</c> mientras el partido no se ha jugado.</summary>
    public Score? Score { get; private set; }

    public IReadOnlyList<Goal> Goals => _goals.AsReadOnly();

    /// <summary>Programa un partido. Que ambos equipos existan lo valida el handler.</summary>
    public static Result<Match> Schedule(Guid homeTeamId, Guid awayTeamId, DateTime scheduledAtUtc)
    {
        if (homeTeamId == awayTeamId)
        {
            return MatchErrors.SameTeams;
        }

        return Result.Success(new Match(Guid.CreateVersion7(), homeTeamId, awayTeamId, scheduledAtUtc));
    }

    /// <summary>
    /// Registra o corrige el resultado (semántica de PUT: reemplaza el resultado completo).
    /// Lista vacía = 0 a 0.
    /// </summary>
    /// <remarks>
    /// Idempotente: si llega exactamente el mismo resultado no cambia nada ni se emite evento,
    /// así un reintento no altera la tabla de posiciones.
    /// </remarks>
    public Result RegisterResult(IReadOnlyCollection<GoalScorer> scorers)
    {
        if (scorers.Any(s => s.TeamId != HomeTeamId && s.TeamId != AwayTeamId))
        {
            return MatchErrors.ScorerNotInMatch;
        }

        if (Status == MatchStatus.Played && HasSameScorers(scorers))
        {
            return Result.Success();
        }

        var previousScore = Score;

        _goals.Clear();
        _goals.AddRange(scorers.Select(scorer => Goal.Create(Id, scorer)));

        Score = new Score(
            HomeGoals: scorers.Count(s => s.TeamId == HomeTeamId),
            AwayGoals: scorers.Count(s => s.TeamId == AwayTeamId));
        Status = MatchStatus.Played;

        Raise(new MatchResultRegisteredDomainEvent(Id, HomeTeamId, AwayTeamId, Score, previousScore));

        return Result.Success();
    }

    // Compara como multiconjunto: no importa el orden, sí cuántos goles hizo cada jugador.
    private bool HasSameScorers(IReadOnlyCollection<GoalScorer> scorers) =>
        _goals.Select(g => g.PlayerId).Order()
            .SequenceEqual(scorers.Select(s => s.PlayerId).Order());
}
