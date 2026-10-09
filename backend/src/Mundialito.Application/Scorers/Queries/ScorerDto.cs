namespace Mundialito.Application.Scorers.Queries;

/// <summary>Fila de la tabla de goleadores. Solo aparecen jugadores con al menos un gol.</summary>
public sealed record ScorerDto
{
    /// <summary>Posición por cantidad de goles en todo el torneo. Mismos goles = posición compartida.</summary>
    public int Position { get; init; }

    public Guid PlayerId { get; init; }

    public string PlayerName { get; init; } = string.Empty;

    public int ShirtNumber { get; init; }

    public Guid TeamId { get; init; }

    public string TeamName { get; init; } = string.Empty;

    public int Goals { get; init; }
}
