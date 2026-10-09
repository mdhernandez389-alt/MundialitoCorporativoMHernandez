namespace Mundialito.Application.Standings.Queries;

/// <summary>Fila de la tabla de posiciones.</summary>
public sealed record StandingDto
{
    /// <summary>
    /// Posición oficial (puntos → diferencia de gol → goles a favor), calculada sobre todo el torneo:
    /// no cambia al filtrar ni al ordenar por otra columna. Empate total = posición compartida.
    /// </summary>
    public int Position { get; init; }

    public Guid TeamId { get; init; }

    public string TeamName { get; init; } = string.Empty;

    public string Department { get; init; } = string.Empty;

    public int Played { get; init; }

    public int Won { get; init; }

    public int Drawn { get; init; }

    public int Lost { get; init; }

    public int GoalsFor { get; init; }

    public int GoalsAgainst { get; init; }

    public int GoalDifference { get; init; }

    public int Points { get; init; }
}
