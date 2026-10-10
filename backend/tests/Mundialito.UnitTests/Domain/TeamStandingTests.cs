using Mundialito.Domain.Standings;

namespace Mundialito.UnitTests.Domain;

/// <summary>Cálculo de puntos (3/1/0) y diferencia de gol de una fila de la tabla (§15).</summary>
public sealed class TeamStandingTests
{
    private static TeamStanding NewStanding() => TeamStanding.Create(Guid.NewGuid());

    [Fact]
    public void Una_fila_nueva_empieza_en_cero()
    {
        var standing = NewStanding();

        Assert.Equal(0, standing.Played);
        Assert.Equal(0, standing.Points);
        Assert.Equal(0, standing.GoalDifference);
    }

    [Fact]
    public void Victoria_suma_tres_puntos()
    {
        var standing = NewStanding();

        standing.ApplyResult(goalsFor: 2, goalsAgainst: 0);

        Assert.Equal(3, standing.Points);
        Assert.Equal(1, standing.Won);
        Assert.Equal(1, standing.Played);
    }

    [Fact]
    public void Empate_suma_un_punto()
    {
        var standing = NewStanding();

        standing.ApplyResult(goalsFor: 1, goalsAgainst: 1);

        Assert.Equal(1, standing.Points);
        Assert.Equal(1, standing.Drawn);
    }

    [Fact]
    public void Derrota_no_suma_puntos()
    {
        var standing = NewStanding();

        standing.ApplyResult(goalsFor: 0, goalsAgainst: 3);

        Assert.Equal(0, standing.Points);
        Assert.Equal(1, standing.Lost);
    }

    [Fact]
    public void Los_puntos_combinan_victorias_y_empates()
    {
        var standing = NewStanding();

        standing.ApplyResult(2, 0);   // victoria: 3
        standing.ApplyResult(1, 0);   // victoria: 3
        standing.ApplyResult(2, 2);   // empate:   1
        standing.ApplyResult(0, 1);   // derrota:  0

        Assert.Equal(7, standing.Points);
        Assert.Equal(4, standing.Played);
    }

    [Theory]
    [InlineData(3, 1, 2)]
    [InlineData(0, 4, -4)]
    [InlineData(2, 2, 0)]
    public void La_diferencia_de_gol_es_goles_a_favor_menos_en_contra(int goalsFor, int goalsAgainst, int expected)
    {
        var standing = NewStanding();

        standing.ApplyResult(goalsFor, goalsAgainst);

        Assert.Equal(expected, standing.GoalDifference);
    }

    [Fact]
    public void La_diferencia_de_gol_se_acumula_entre_partidos()
    {
        var standing = NewStanding();

        standing.ApplyResult(3, 1);   // +2
        standing.ApplyResult(0, 4);   // -4

        Assert.Equal(3, standing.GoalsFor);
        Assert.Equal(5, standing.GoalsAgainst);
        Assert.Equal(-2, standing.GoalDifference);
    }

    [Fact]
    public void Revertir_deshace_exactamente_lo_aplicado()
    {
        var standing = NewStanding();
        standing.ApplyResult(3, 1);

        standing.RevertResult(3, 1);

        Assert.Equal(0, standing.Played);
        Assert.Equal(0, standing.Won);
        Assert.Equal(0, standing.Points);
        Assert.Equal(0, standing.GoalsFor);
        Assert.Equal(0, standing.GoalsAgainst);
    }
}
