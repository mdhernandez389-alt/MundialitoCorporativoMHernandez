using Dapper;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;
using Mundialito.Application.Players.Queries;
using Mundialito.Application.Players.Queries.GetPlayerById;
using Mundialito.Application.Players.Queries.GetTeamPlayers;
using Mundialito.Domain.Players;
using Mundialito.Domain.Results;
using Mundialito.Domain.Teams;
using Mundialito.Infrastructure.Queries.Common;

namespace Mundialito.Infrastructure.Queries.Players;

internal sealed class GetTeamPlayersHandler(DbConnectionFactory connections)
    : IQueryHandler<GetTeamPlayersQuery, PagedResult<PlayerDto>>
{
    private static readonly SortMap Sorting = new(
        defaultSortBy: "shirtNumber",
        tieBreaker: "p.Id",
        new Dictionary<string, string>
        {
            ["shirtNumber"] = "p.ShirtNumber",
            ["fullName"] = "p.FullName",
        });

    public async Task<Result<PagedResult<PlayerDto>>> HandleAsync(
        GetTeamPlayersQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();

        // Equipo inexistente → 404 (no una lista vacía, que haría creer que existe pero no tiene jugadores).
        var teamExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS (SELECT 1 FROM Teams WHERE Id = @TeamId) THEN 1 ELSE 0 END",
            new { query.TeamId },
            cancellationToken: cancellationToken));

        if (!teamExists)
        {
            return TeamErrors.NotFound(query.TeamId);
        }

        var where = new SqlWhere()
            .Add("p.TeamId = @TeamId", "TeamId", query.TeamId)
            .AddContains("p.FullName", "Name", query.Name);

        return await PagedSql.QueryAsync<PlayerDto>(
            connection,
            query,
            Sorting,
            select: "SELECT p.Id, p.TeamId, p.FullName, p.ShirtNumber",
            from: "FROM Players p",
            where,
            cancellationToken);
    }
}

internal sealed class GetPlayerByIdHandler(DbConnectionFactory connections)
    : IQueryHandler<GetPlayerByIdQuery, PlayerDto>
{
    public async Task<Result<PlayerDto>> HandleAsync(GetPlayerByIdQuery query, CancellationToken cancellationToken)
    {
        // El TeamId también filtra: /teams/A/players/{jugador de B} no existe.
        const string sql = """
            SELECT p.Id, p.TeamId, p.FullName, p.ShirtNumber
            FROM Players p
            WHERE p.Id = @PlayerId AND p.TeamId = @TeamId
            """;

        await using var connection = connections.Create();
        var player = await connection.QuerySingleOrDefaultAsync<PlayerDto>(
            new CommandDefinition(sql, new { query.PlayerId, query.TeamId }, cancellationToken: cancellationToken));

        return player is null ? PlayerErrors.NotFound(query.PlayerId) : Result.Success(player);
    }
}
