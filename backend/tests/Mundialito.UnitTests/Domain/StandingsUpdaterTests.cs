using Mundialito.Domain.Matches;
using Mundialito.Domain.Standings;

namespace Mundialito.UnitTests.Domain;

/// <summary>Tabla de posiciones al registrar, repetir o corregir un resultado (§15).</summary>
public sealed class StandingsUpdaterTests
{
    private readonly TeamStanding _home = TeamStanding.Create(Guid.NewGuid());
    private readonly TeamStanding _away = TeamStanding.Create(Guid.NewGuid());

    [Fact]
    public void El_primer_registro_aplica_el_resultado_a_cada_equipo_desde_su_lado()
    {
        StandingsUpdater.ApplyResultChange(_home, _away, previousScore: null, new Score(HomeGoals: 2, AwayGoals: 1));

        // Local: ganó 2-1.
        Assert.Equal(3, _home.Points);
        Assert.Equal(2, _home.GoalsFor);
        Assert.Equal(1, _home.GoalsAgainst);

        // Visitante: perdió 1-2 (sus goles a favor son los AwayGoals).
        Assert.Equal(0, _away.Points);
        Assert.Equal(1, _away.GoalsFor);
        Assert.Equal(2, _away.GoalsAgainst);
    }

    [Fact]
    public void Un_reintento_identico_no_cambia_la_tabla()
    {
        StandingsUpdater.ApplyResultChange(_home, _away, null, new Score(2, 1));

        StandingsUpdater.ApplyResultChange(_home, _away, new Score(2, 1), new Score(2, 1));

        Assert.Equal(1, _home.Played);
        Assert.Equal(3, _home.Points);
        Assert.Equal(1, _away.Played);
    }

    [Fact]
    public void Una_correccion_revierte_el_resultado_anterior_y_aplica_el_nuevo()
    {
        StandingsUpdater.ApplyResultChange(_home, _away, null, new Score(2, 1));

        StandingsUpdater.ApplyResultChange(_home, _away, new Score(2, 1), new Score(1, 1));

        // Si el 2-1 no se revirtiera, el local tendría 2 jugados y 4 puntos (victoria + empate).
        Assert.Equal(1, _home.Played);
        Assert.Equal(1, _home.Points);
        Assert.Equal(1, _away.Played);
        Assert.Equal(1, _away.Points);
    }

    [Fact]
    public void Dos_correcciones_encadenadas_dejan_un_solo_partido_jugado()
    {
        StandingsUpdater.ApplyResultChange(_home, _away, null, new Score(2, 1));
        StandingsUpdater.ApplyResultChange(_home, _away, new Score(2, 1), new Score(1, 1));

        StandingsUpdater.ApplyResultChange(_home, _away, new Score(1, 1), new Score(0, 3));

        Assert.Equal(1, _home.Played);
        Assert.Equal(1, _home.Lost);
        Assert.Equal(-3, _home.GoalDifference);
        Assert.Equal(1, _away.Won);
        Assert.Equal(3, _away.Points);
    }
}
