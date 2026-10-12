using Mundialito.Domain.Matches;

namespace Mundialito.UnitTests.Domain;

/// <summary>Registro de resultados: el marcador se deriva de los goles y el PUT es idempotente.</summary>
public sealed class MatchTests
{
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid Away = Guid.NewGuid();
    private static readonly Guid HomeStriker = Guid.NewGuid();
    private static readonly Guid AwayStriker = Guid.NewGuid();

    private static Match NewMatch() => Match.Schedule(Home, Away, DateTime.UtcNow).Value;

    private static GoalScorer HomeGoal => new(HomeStriker, Home);

    private static GoalScorer AwayGoal => new(AwayStriker, Away);

    private static GoalScorer HomeOwnGoal => new(HomeStriker, Home, IsOwnGoal: true);

    private static GoalScorer AwayOwnGoal => new(AwayStriker, Away, IsOwnGoal: true);

    [Fact]
    public void Un_equipo_no_puede_jugar_contra_si_mismo()
    {
        var result = Match.Schedule(Home, Home, DateTime.UtcNow);

        Assert.Equal(MatchErrors.SameTeams, result.Error);
    }

    [Fact]
    public void El_marcador_se_deriva_de_los_goleadores()
    {
        var match = NewMatch();

        match.RegisterResult([HomeGoal, HomeGoal, AwayGoal]);

        Assert.Equal(new Score(2, 1), match.Score);
        Assert.Equal(MatchStatus.Played, match.Status);
        Assert.Equal(3, match.Goals.Count);
    }

    [Fact]
    public void Sin_goleadores_es_cero_a_cero()
    {
        var match = NewMatch();

        match.RegisterResult([]);

        Assert.Equal(new Score(0, 0), match.Score);
    }

    [Fact]
    public void Registrar_un_resultado_emite_el_evento_sin_marcador_previo()
    {
        var match = NewMatch();

        match.RegisterResult([HomeGoal]);

        var domainEvent = Assert.IsType<MatchResultRegisteredDomainEvent>(Assert.Single(match.DomainEvents));
        Assert.Equal(new Score(1, 0), domainEvent.Score);
        Assert.Null(domainEvent.PreviousScore);
    }

    [Fact]
    public void El_mismo_resultado_en_otro_orden_no_cambia_nada_ni_emite_evento()
    {
        var match = NewMatch();
        match.RegisterResult([HomeGoal, AwayGoal, HomeGoal]);
        match.ClearDomainEvents();

        var result = match.RegisterResult([AwayGoal, HomeGoal, HomeGoal]);

        Assert.True(result.IsSuccess);
        Assert.Empty(match.DomainEvents);
    }

    [Fact]
    public void Una_correccion_emite_el_evento_con_el_marcador_anterior()
    {
        var match = NewMatch();
        match.RegisterResult([HomeGoal, HomeGoal, AwayGoal]);
        match.ClearDomainEvents();

        match.RegisterResult([HomeGoal, AwayGoal]);

        var domainEvent = Assert.IsType<MatchResultRegisteredDomainEvent>(Assert.Single(match.DomainEvents));
        Assert.Equal(new Score(1, 1), domainEvent.Score);
        Assert.Equal(new Score(2, 1), domainEvent.PreviousScore);
    }

    [Fact]
    public void Un_goleador_de_otro_equipo_es_rechazado()
    {
        var match = NewMatch();

        var result = match.RegisterResult([new GoalScorer(Guid.NewGuid(), TeamId: Guid.NewGuid())]);

        Assert.Equal(MatchErrors.ScorerNotInMatch, result.Error);
        Assert.Null(match.Score);
    }

    [Fact]
    public void Un_autogol_suma_al_rival_del_jugador()
    {
        var match = NewMatch();

        match.RegisterResult([HomeGoal, HomeOwnGoal, AwayOwnGoal]);

        Assert.Equal(new Score(2, 1), match.Score);
        var ownGoal = Assert.Single(match.Goals, g => g.IsOwnGoal && g.PlayerId == HomeStriker);
        Assert.Equal(Away, ownGoal.TeamId);
    }

    [Fact]
    public void Un_autogol_de_un_jugador_ajeno_al_partido_es_rechazado()
    {
        var match = NewMatch();

        var result = match.RegisterResult([new GoalScorer(Guid.NewGuid(), Guid.NewGuid(), IsOwnGoal: true)]);

        Assert.Equal(MatchErrors.ScorerNotInMatch, result.Error);
    }

    [Fact]
    public void Corregir_un_gol_por_un_autogol_del_mismo_jugador_cambia_el_resultado()
    {
        var match = NewMatch();
        match.RegisterResult([HomeGoal]);
        match.ClearDomainEvents();

        match.RegisterResult([HomeOwnGoal]);

        Assert.Equal(new Score(0, 1), match.Score);
        var domainEvent = Assert.IsType<MatchResultRegisteredDomainEvent>(Assert.Single(match.DomainEvents));
        Assert.Equal(new Score(1, 0), domainEvent.PreviousScore);
    }

    [Fact]
    public void El_mismo_resultado_con_autogoles_en_otro_orden_no_emite_evento()
    {
        var match = NewMatch();
        match.RegisterResult([HomeGoal, HomeOwnGoal, AwayGoal]);
        match.ClearDomainEvents();

        match.RegisterResult([AwayGoal, HomeGoal, HomeOwnGoal]);

        Assert.Empty(match.DomainEvents);
    }
}
