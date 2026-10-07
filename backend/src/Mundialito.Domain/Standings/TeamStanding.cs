namespace Mundialito.Domain.Standings;

/// <summary>
/// Fila de la tabla de posiciones de un equipo. Se guarda en la base y se actualiza en la misma
/// transacción que registra el resultado, así Dapper solo tiene que leerla y ordenarla.
/// </summary>
/// <remarks>
/// Orden de la tabla: <see cref="Points"/>, luego <see cref="GoalDifference"/>, luego <see cref="GoalsFor"/>.
/// </remarks>
public sealed class TeamStanding
{
    public const int PointsPerWin = 3;
    public const int PointsPerDraw = 1;

    private TeamStanding(Guid teamId)
    {
        TeamId = teamId;
    }

    public Guid TeamId { get; }

    public int Played { get; private set; }

    public int Won { get; private set; }

    public int Drawn { get; private set; }

    public int Lost { get; private set; }

    public int GoalsFor { get; private set; }

    public int GoalsAgainst { get; private set; }

    public int Points => (Won * PointsPerWin) + (Drawn * PointsPerDraw);

    public int GoalDifference => GoalsFor - GoalsAgainst;

    /// <summary>Fila en cero: un equipo recién creado aparece en la tabla desde el inicio.</summary>
    public static TeamStanding Create(Guid teamId) => new(teamId);

    /// <summary>Suma un partido jugado, visto desde este equipo.</summary>
    public void ApplyResult(int goalsFor, int goalsAgainst) => Tally(goalsFor, goalsAgainst, direction: +1);

    /// <summary>Deshace un partido sumado antes (cuando se corrige un resultado).</summary>
    public void RevertResult(int goalsFor, int goalsAgainst) => Tally(goalsFor, goalsAgainst, direction: -1);

    // direction = +1 suma el partido; -1 lo resta. Una sola regla para aplicar y revertir:
    // imposible que ambas versiones se desincronicen.
    private void Tally(int goalsFor, int goalsAgainst, int direction)
    {
        Played += direction;
        GoalsFor += goalsFor * direction;
        GoalsAgainst += goalsAgainst * direction;

        if (goalsFor == goalsAgainst)
        {
            Drawn += direction;
        }
        else if (goalsFor > goalsAgainst)
        {
            Won += direction;
        }
        else
        {
            Lost += direction;
        }
    }
}
