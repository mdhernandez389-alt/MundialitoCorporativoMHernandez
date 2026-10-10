using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Abstractions.Querying;
using Mundialito.Application.Standings;
using Mundialito.Application.Standings.Queries;
using Mundialito.Application.Standings.Queries.GetStandings;
using Mundialito.Application.Teams;
using Mundialito.Domain.Standings;
using Mundialito.Domain.Teams;

namespace Mundialito.IntegrationTests.Queries;

/// <summary>Orden oficial de la tabla (§8, §15): el SQL de Dapper con el RANK() de OfficialOrder.</summary>
[Collection(nameof(SqlServerCollection))]
public sealed class StandingsQueryTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task A_igualdad_de_puntos_gana_la_diferencia_de_gol_aunque_el_otro_tenga_mas_goles_a_favor()
    {
        var tag = SqlServerFixture.UniqueName("Tabla");
        await CreateTeamWithResultsAsync($"{tag} Sistemas", (6, 4), (4, 3));   // 6 pts, GF 10, DG +3
        await CreateTeamWithResultsAsync($"{tag} Finanzas", (3, 0), (2, 0));   // 6 pts, GF 5,  DG +5

        var rows = await GetStandingsAsync(tag);

        Assert.True(Position(rows, "Finanzas") < Position(rows, "Sistemas"));
    }

    [Fact]
    public async Task A_igualdad_de_puntos_y_diferencia_gana_quien_tiene_mas_goles_a_favor()
    {
        var tag = SqlServerFixture.UniqueName("Tabla");
        await CreateTeamWithResultsAsync($"{tag} Legal", (3, 1));    // 3 pts, DG +2, GF 3
        await CreateTeamWithResultsAsync($"{tag} Ventas", (2, 0));   // 3 pts, DG +2, GF 2

        var rows = await GetStandingsAsync(tag);

        Assert.True(Position(rows, "Legal") < Position(rows, "Ventas"));
    }

    [Fact]
    public async Task Un_empate_total_comparte_la_posicion()
    {
        var tag = SqlServerFixture.UniqueName("Tabla");
        await CreateTeamWithResultsAsync($"{tag} Compras", (1, 1));
        await CreateTeamWithResultsAsync($"{tag} Auditoria", (1, 1));

        var rows = await GetStandingsAsync(tag);

        Assert.Equal(Position(rows, "Compras"), Position(rows, "Auditoria"));
    }

    [Fact]
    public async Task Ordenar_por_otra_columna_no_cambia_la_posicion_oficial()
    {
        var tag = SqlServerFixture.UniqueName("Tabla");
        await CreateTeamWithResultsAsync($"{tag} Sistemas", (6, 4), (4, 3));
        await CreateTeamWithResultsAsync($"{tag} Finanzas", (3, 0), (2, 0));

        var byGoalsFor = await GetStandingsAsync(tag, sortBy: "goalsFor", sortDirection: "desc");
        var official = await GetStandingsAsync(tag);

        Assert.EndsWith("Sistemas", byGoalsFor[0].TeamName);
        Assert.Equal(Position(official, "Sistemas"), Position(byGoalsFor, "Sistemas"));
    }

    /// <summary>Equipo con su fila de tabla ya cargada (los resultados se aplican con la regla real del dominio).</summary>
    private async Task CreateTeamWithResultsAsync(string name, params (int GoalsFor, int GoalsAgainst)[] results)
    {
        await using var scope = fixture.CreateScope();
        var team = Team.Create(name, "Pruebas").Value;
        var standing = TeamStanding.Create(team.Id);
        foreach (var (goalsFor, goalsAgainst) in results)
        {
            standing.ApplyResult(goalsFor, goalsAgainst);
        }

        scope.ServiceProvider.GetRequiredService<ITeamRepository>().Add(team);
        scope.ServiceProvider.GetRequiredService<ITeamStandingRepository>().Add(standing);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
    }

    private async Task<IReadOnlyList<StandingDto>> GetStandingsAsync(string tag, string? sortBy = null, string? sortDirection = null)
    {
        await using var scope = fixture.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<GetStandingsQuery, PagedResult<StandingDto>>>();

        var result = await handler.HandleAsync(
            new GetStandingsQuery { TeamName = tag, SortBy = sortBy, SortDirection = sortDirection },
            CancellationToken.None);

        return result.Value.Data;
    }

    private static int Position(IReadOnlyList<StandingDto> rows, string teamSuffix) =>
        rows.Single(r => r.TeamName.EndsWith(teamSuffix, StringComparison.Ordinal)).Position;
}
