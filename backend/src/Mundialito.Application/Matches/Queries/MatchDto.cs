namespace Mundialito.Application.Matches.Queries;

/// <summary>Partido para listados: trae los nombres de los equipos para no obligar al cliente a pedirlos aparte.</summary>
public record MatchDto
{
    public Guid Id { get; init; }

    public Guid HomeTeamId { get; init; }

    public string HomeTeamName { get; init; } = string.Empty;

    public Guid AwayTeamId { get; init; }

    public string AwayTeamName { get; init; } = string.Empty;

    /// <summary>
    /// La base guarda UTC pero devuelve la fecha "sin zona". Se marca como UTC para que el JSON lleve la "Z"
    /// (<c>2026-10-11T00:00:00Z</c>) y el cliente no la confunda con su hora local.
    /// </summary>
    public DateTime ScheduledAtUtc
    {
        get => _scheduledAtUtc;
        init => _scheduledAtUtc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private readonly DateTime _scheduledAtUtc;

    /// <summary><c>Scheduled</c> o <c>Played</c>.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary><c>null</c> mientras el partido no se ha jugado.</summary>
    public int? HomeGoals { get; init; }

    public int? AwayGoals { get; init; }
}

/// <summary>Detalle de un partido: lo del listado más sus goles (para mostrar o corregir el resultado).</summary>
public sealed record MatchDetailDto : MatchDto
{
    public IReadOnlyList<GoalDto> Goals { get; init; } = [];
}

public sealed record GoalDto
{
    public Guid PlayerId { get; init; }

    public string PlayerName { get; init; } = string.Empty;

    public Guid TeamId { get; init; }
}
