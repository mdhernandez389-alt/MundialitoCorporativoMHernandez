using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;
using Mundialito.Application.Scorers.Queries;
using Mundialito.Application.Scorers.Queries.GetScorers;
using Mundialito.Domain.Results;
using Mundialito.Infrastructure.Queries.Common;

namespace Mundialito.Infrastructure.Queries.Scorers;

/// <summary>
/// Tabla de goleadores (§8). Se calcula al vuelo agrupando <c>Goals</c> (con índice por jugador):
/// como el marcador se deriva de los goles, esta tabla y la de posiciones nunca se contradicen.
/// </summary>
internal sealed class GetScorersHandler(DbConnectionFactory connections)
    : IQueryHandler<GetScorersQuery, PagedResult<ScorerDto>>
{
    // Agrupa y rankea sobre TODO el torneo antes de filtrar: el goleador de un equipo conserva su posición real.
    // RANK sobre COUNT(*): mismos goles = misma posición.
    private const string From = """
        FROM (
            SELECT p.Id AS PlayerId, p.FullName AS PlayerName, p.ShirtNumber,
                   p.TeamId, t.Name AS TeamName,
                   COUNT(*) AS Goals,
                   RANK() OVER (ORDER BY COUNT(*) DESC) AS Position
            FROM Goals g
            JOIN Players p ON p.Id = g.PlayerId
            JOIN Teams t ON t.Id = p.TeamId
            GROUP BY p.Id, p.FullName, p.ShirtNumber, p.TeamId, t.Name
        ) sc
        """;

    private const string Select = """
        SELECT sc.Position, sc.PlayerId, sc.PlayerName, sc.ShirtNumber, sc.TeamId, sc.TeamName, sc.Goals
        """;

    private static readonly SortMap Sorting = new(
        defaultSortBy: "position",
        tieBreaker: "sc.PlayerName, sc.PlayerId",
        new Dictionary<string, string>
        {
            ["position"] = "sc.Position",
            ["goals"] = "sc.Goals",
            ["playerName"] = "sc.PlayerName",
            ["teamName"] = "sc.TeamName",
        });

    public async Task<Result<PagedResult<ScorerDto>>> HandleAsync(GetScorersQuery query, CancellationToken cancellationToken)
    {
        var where = new SqlWhere()
            .AddContains("sc.PlayerName", "PlayerName", query.PlayerName);

        if (query.TeamId is { } teamId)
        {
            where.Add("sc.TeamId = @TeamId", "TeamId", teamId);
        }

        await using var connection = connections.Create();

        return await PagedSql.QueryAsync<ScorerDto>(
            connection,
            query,
            Sorting,
            Select,
            From,
            where,
            cancellationToken);
    }
}
