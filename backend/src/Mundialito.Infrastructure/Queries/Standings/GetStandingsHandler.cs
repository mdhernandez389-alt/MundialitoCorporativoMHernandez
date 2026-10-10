using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;
using Mundialito.Application.Standings.Queries;
using Mundialito.Application.Standings.Queries.GetStandings;
using Mundialito.Domain.Results;
using Mundialito.Domain.Standings;
using Mundialito.Infrastructure.Queries.Common;

namespace Mundialito.Infrastructure.Queries.Standings;

/// <summary>
/// Tabla de posiciones (§8). Lee <c>TeamStandings</c>, que ya está al día (se actualiza en la misma
/// transacción que cada resultado): el costo no depende de cuántos partidos se jugaron.
/// </summary>
internal sealed class GetStandingsHandler(DbConnectionFactory connections)
    : IQueryHandler<GetStandingsQuery, PagedResult<StandingDto>>
{
    /// <summary>Criterio oficial del §8 para calcular la posición. Columnas disponibles: ver el FROM.</summary>
    private const string OfficialOrder = "calc.Points DESC, calc.GoalDifference DESC, s.GoalsFor DESC";

    // La posición se calcula en una subconsulta sobre TODOS los equipos, antes de filtrar o paginar:
    // filtrar por nombre no convierte al 5º en 1º.
    // CROSS APPLY calcula Points y GoalDifference una vez, para usarlos en el RANK y en el SELECT.
    // La regla 3/1/0 sale de las constantes del dominio: no se duplica en el SQL.
    private static readonly string From = $"""
        FROM (
            SELECT s.TeamId, t.Name AS TeamName, t.Department,
                   s.Played, s.Won, s.Drawn, s.Lost, s.GoalsFor, s.GoalsAgainst,
                   calc.GoalDifference, calc.Points,
                   RANK() OVER (ORDER BY {OfficialOrder}) AS Position
            FROM TeamStandings s
            JOIN Teams t ON t.Id = s.TeamId
            CROSS APPLY (
                SELECT s.GoalsFor - s.GoalsAgainst AS GoalDifference,
                       s.Won * {TeamStanding.PointsPerWin} + s.Drawn * {TeamStanding.PointsPerDraw} AS Points
            ) calc
        ) st
        """;

    private const string Select = """
        SELECT st.Position, st.TeamId, st.TeamName, st.Department,
               st.Played, st.Won, st.Drawn, st.Lost,
               st.GoalsFor, st.GoalsAgainst, st.GoalDifference, st.Points
        """;

    // RANK da la misma posición a un empate total; dentro del empate se lista por nombre para que el orden sea estable.
    private static readonly SortMap Sorting = new(
        defaultSortBy: "position",
        tieBreaker: "st.TeamName, st.TeamId",
        new Dictionary<string, string>
        {
            ["position"] = "st.Position",
            ["points"] = "st.Points",
            ["goalDifference"] = "st.GoalDifference",
            ["goalsFor"] = "st.GoalsFor",
            ["teamName"] = "st.TeamName",
        });

    public async Task<Result<PagedResult<StandingDto>>> HandleAsync(
        GetStandingsQuery query,
        CancellationToken cancellationToken)
    {
        var where = new SqlWhere()
            .AddContains("st.TeamName", "TeamName", query.TeamName)
            .AddContains("st.Department", "Department", query.Department);

        await using var connection = connections.Create();

        return await PagedSql.QueryAsync<StandingDto>(
            connection,
            query,
            Sorting,
            Select,
            From,
            where,
            cancellationToken);
    }
}
