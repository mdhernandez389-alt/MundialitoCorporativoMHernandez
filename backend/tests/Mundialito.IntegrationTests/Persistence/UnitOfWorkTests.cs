using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Matches;
using Mundialito.Application.Players;
using Mundialito.Application.Standings;
using Mundialito.Application.Teams;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Players;
using Mundialito.Domain.Standings;
using Mundialito.Domain.Teams;
using Mundialito.Infrastructure.Persistence;

namespace Mundialito.IntegrationTests.Persistence;

/// <summary>Comportamiento del commit del Unit of Work (§4, §15) contra SQL Server real.</summary>
[Collection(nameof(SqlServerCollection))]
public sealed class UnitOfWorkTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task El_commit_confirma_juntos_los_cambios_de_varios_repositorios()
    {
        var team = Team.Create(SqlServerFixture.UniqueName("Sistemas"), "TI").Value;

        await using (var scope = fixture.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITeamRepository>().Add(team);
            scope.ServiceProvider.GetRequiredService<ITeamStandingRepository>().Add(TeamStanding.Create(team.Id));

            var commit = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);

            Assert.True(commit.IsSuccess);
        }

        // Otro scope (otro "request"): lo leído viene de la base, no de la memoria del DbContext anterior.
        await using var check = fixture.CreateScope();
        var dbContext = check.ServiceProvider.GetRequiredService<MundialitoDbContext>();
        Assert.True(await dbContext.Teams.AnyAsync(t => t.Id == team.Id));
        Assert.True(await dbContext.TeamStandings.AnyAsync(s => s.TeamId == team.Id));
    }

    [Fact]
    public async Task Sin_commit_explicito_no_se_guarda_nada()
    {
        var team = Team.Create(SqlServerFixture.UniqueName("Sin commit"), "TI").Value;

        await using (var scope = fixture.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITeamRepository>().Add(team);
            // El scope termina sin CommitAsync.
        }

        await using var check = fixture.CreateScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<MundialitoDbContext>().Teams.AnyAsync(t => t.Id == team.Id));
    }

    [Fact]
    public async Task Guardar_desde_el_DbContext_fuera_del_UnitOfWork_esta_bloqueado()
    {
        await using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MundialitoDbContext>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
        Assert.Throws<InvalidOperationException>(() => dbContext.SaveChanges());
    }

    [Fact]
    public async Task Un_duplicado_detectado_por_la_base_devuelve_409_de_negocio_y_hace_rollback_de_todo()
    {
        var name = SqlServerFixture.UniqueName("Finanzas");
        await fixture.CreateTeamAsync(name);

        // Simula la carrera: el handler no vio el duplicado y la base lo rechaza en el commit.
        var duplicate = Team.Create(name, "Otro").Value;
        await using (var scope = fixture.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITeamRepository>().Add(duplicate);
            scope.ServiceProvider.GetRequiredService<ITeamStandingRepository>().Add(TeamStanding.Create(duplicate.Id));

            var commit = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);

            // Error 2601 de SQL Server → el mismo código que daría el handler, sin excepción.
            Assert.Equal(TeamErrors.NameDuplicated, commit.Error);
        }

        // Rollback: tampoco quedó la fila de tabla (ni el evento) del equipo rechazado.
        await using var check = fixture.CreateScope();
        var dbContext = check.ServiceProvider.GetRequiredService<MundialitoDbContext>();
        Assert.False(await dbContext.TeamStandings.AnyAsync(s => s.TeamId == duplicate.Id));
        Assert.False(await dbContext.DomainEvents.AnyAsync(e => e.Payload.Contains(duplicate.Id.ToString())));
    }

    [Fact]
    public async Task El_commit_guarda_los_eventos_de_dominio_con_TraceId_y_CorrelationId()
    {
        var team = Team.Create(SqlServerFixture.UniqueName("Eventos"), "TI").Value;
        var eventId = Assert.Single(team.DomainEvents).EventId;

        using var activity = new Activity("test-request").Start();
        await using (var scope = fixture.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TestCorrelationContext>().CorrelationId = "corr-de-prueba";
            scope.ServiceProvider.GetRequiredService<ITeamRepository>().Add(team);
            scope.ServiceProvider.GetRequiredService<ITeamStandingRepository>().Add(TeamStanding.Create(team.Id));

            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
        }

        await using var check = fixture.CreateScope();
        var record = await check.ServiceProvider.GetRequiredService<MundialitoDbContext>()
            .DomainEvents.SingleAsync(e => e.Id == eventId);

        Assert.Equal(nameof(TeamCreatedDomainEvent), record.Type);
        Assert.Equal(activity.TraceId.ToHexString(), record.TraceId);
        Assert.Equal("corr-de-prueba", record.CorrelationId);
    }

    [Fact]
    public async Task Dos_resultados_simultaneos_del_mismo_partido_el_segundo_recibe_409_y_la_tabla_cuenta_uno()
    {
        var home = await fixture.CreateTeamAsync(SqlServerFixture.UniqueName("Local"));
        var away = await fixture.CreateTeamAsync(SqlServerFixture.UniqueName("Visita"));
        var (striker, matchId) = await CreatePlayerAndMatchAsync(home, away);

        // Dos "requests" cargan el partido y la tabla ANTES de que cualquiera confirme.
        await using var first = fixture.CreateScope();
        await using var second = fixture.CreateScope();
        await RegisterOneZeroAsync(first, matchId, striker, home);
        await RegisterOneZeroAsync(second, matchId, striker, home);

        var firstCommit = await first.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
        var secondCommit = await second.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);

        Assert.True(firstCommit.IsSuccess);
        // rowversion: el segundo cargó una versión vieja del partido → conflicto, sin sumar el partido dos veces.
        Assert.Equal(PersistenceErrors.ConcurrencyConflict, secondCommit.Error);

        await using var check = fixture.CreateScope();
        var homeStanding = await check.ServiceProvider.GetRequiredService<MundialitoDbContext>()
            .TeamStandings.SingleAsync(s => s.TeamId == home.Id);
        Assert.Equal(1, homeStanding.Played);
        Assert.Equal(3, homeStanding.Points);
    }

    private async Task<(Player Striker, Guid MatchId)> CreatePlayerAndMatchAsync(Team home, Team away)
    {
        await using var scope = fixture.CreateScope();
        var striker = Player.Create(home.Id, "Delantero", 9).Value;
        var match = Match.Schedule(home.Id, away.Id, DateTime.UtcNow).Value;
        scope.ServiceProvider.GetRequiredService<IPlayerRepository>().Add(striker);
        scope.ServiceProvider.GetRequiredService<IMatchRepository>().Add(match);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);

        return (striker, match.Id);
    }

    /// <summary>Lo mismo que hace RegisterMatchResultHandler, sin confirmar.</summary>
    private static async Task RegisterOneZeroAsync(AsyncServiceScope scope, Guid matchId, Player striker, Team home)
    {
        var match = (await scope.ServiceProvider.GetRequiredService<IMatchRepository>().GetByIdAsync(matchId, CancellationToken.None))!;
        var standings = scope.ServiceProvider.GetRequiredService<ITeamStandingRepository>();
        var homeStanding = (await standings.GetByTeamIdAsync(match.HomeTeamId, CancellationToken.None))!;
        var awayStanding = (await standings.GetByTeamIdAsync(match.AwayTeamId, CancellationToken.None))!;

        match.RegisterResult([new GoalScorer(striker.Id, home.Id)]);
        StandingsUpdater.ApplyResultChange(homeStanding, awayStanding, previousScore: null, match.Score!);
    }
}
