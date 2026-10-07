using Mundialito.Domain.Matches;

namespace Mundialito.Domain.Standings;

/// <summary>
/// Servicio de dominio: refleja en la tabla de posiciones el registro (o la corrección) de un resultado.
/// </summary>
/// <remarks>
/// Vive en Domain porque es regla pura del torneo: involucra a dos <see cref="TeamStanding"/> y un
/// <see cref="Score"/>, no le pertenece a ninguno solo y se prueba con tests unitarios sin base de datos.
/// </remarks>
public static class StandingsUpdater
{
    /// <param name="home">Fila del equipo local.</param>
    /// <param name="away">Fila del equipo visitante.</param>
    /// <param name="previousScore">Marcador antes de registrar; <c>null</c> si el partido no se había jugado.</param>
    /// <param name="newScore">Marcador recién registrado.</param>
    public static void ApplyResultChange(TeamStanding home, TeamStanding away, Score? previousScore, Score newScore)
    {
        // Reintento idéntico: la tabla ya refleja este marcador. (== de record: por valor y seguro con null.)
        if (previousScore == newScore)
        {
            return;
        }

        // Corrección: deshace el resultado viejo antes de aplicar el nuevo.
        if (previousScore is not null)
        {
            home.RevertResult(previousScore.HomeGoals, previousScore.AwayGoals);
            away.RevertResult(previousScore.AwayGoals, previousScore.HomeGoals);
        }

        // Cada fila lo ve desde su lado: los goles a favor del visitante son los AwayGoals.
        home.ApplyResult(newScore.HomeGoals, newScore.AwayGoals);
        away.ApplyResult(newScore.AwayGoals, newScore.HomeGoals);
    }
}
