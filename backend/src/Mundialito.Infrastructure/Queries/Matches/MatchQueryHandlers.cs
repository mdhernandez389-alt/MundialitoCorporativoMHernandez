using Dapper;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;
using Mundialito.Application.Matches.Queries;
using Mundialito.Application.Matches.Queries.GetMatchById;
using Mundialito.Application.Matches.Queries.GetMatches;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Results;
using Mundialito.Infrastructure.Queries.Common;

namespace Mundialito.Infrastructure.Queries.Matches;

/// <summary>SQL compartido entre el listado y el detalle: los dos devuelven las mismas columnas.</summary>
internal static class MatchSql
{
    public const string Select = """
        SELECT m.Id,
               m.HomeTeamId, home.Name AS HomeTeamName,
               m.AwayTeamId, away.Name AS AwayTeamName,
               m.ScheduledAtUtc, m.Status, m.HomeGoals, m.AwayGoals
        """;

    // Un solo JOIN por equipo: los nombres llegan en la misma consulta, sin N+1.
    public const string From = """
        FROM Matches m
        JOIN Teams home ON home.Id = m.HomeTeamId
        JOIN Teams away ON away.Id = m.AwayTeamId
        """;
}

internal sealed class GetMatchesHandler(DbConnectionFactory connections)
    : IQueryHandler<GetMatchesQuery, PagedResult<MatchDto>>
{
    private static readonly SortMap Sorting = new(
        defaultSortBy: "scheduledAt",
        tieBreaker: "m.Id",
        new Dictionary<string, string>
        {
            ["scheduledAt"] = "m.ScheduledAtUtc",
            ["status"] = "m.Status",
        });

    public async Task<Result<PagedResult<MatchDto>>> HandleAsync(GetMatchesQuery query, CancellationToken cancellationToken)
    {
        if (query.From > query.To)
        {
            return MatchQueryErrors.InvalidDateRange;
        }

        var where = new SqlWhere();

        if (query.From is { } from)
        {
            where.Add("m.ScheduledAtUtc >= @From", "From", from.UtcDateTime);
        }

        if (query.To is { } to)
        {
            where.Add("m.ScheduledAtUtc <= @To", "To", to.UtcDateTime);
        }

        if (query.TeamId is { } teamId)
        {
            where.Add("(m.HomeTeamId = @TeamId OR m.AwayTeamId = @TeamId)", "TeamId", teamId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            // Se compara contra los nombres del enum (no Enum.TryParse, que aceptaría "1" o "99").
            var status = Enum.GetNames<MatchStatus>()
                .FirstOrDefault(name => name.Equals(query.Status.Trim(), StringComparison.OrdinalIgnoreCase));

            if (status is null)
            {
                return MatchQueryErrors.InvalidStatus;
            }

            where.Add("m.Status = @Status", "Status", status);
        }

        await using var connection = connections.Create();

        return await PagedSql.QueryAsync<MatchDto>(
            connection,
            query,
            Sorting,
            MatchSql.Select,
            MatchSql.From,
            where,
            cancellationToken);
    }
}

internal sealed class GetMatchByIdHandler(DbConnectionFactory connections)
    : IQueryHandler<GetMatchByIdQuery, MatchDetailDto>
{
    public async Task<Result<MatchDetailDto>> HandleAsync(GetMatchByIdQuery query, CancellationToken cancellationToken)
    {
        // Partido y goles en un solo viaje.
        const string sql = $"""
            {MatchSql.Select}
            {MatchSql.From}
            WHERE m.Id = @MatchId;

            SELECT g.PlayerId, p.FullName AS PlayerName, g.TeamId, g.IsOwnGoal
            FROM Goals g
            JOIN Players p ON p.Id = g.PlayerId
            WHERE g.MatchId = @MatchId
            ORDER BY g.TeamId, p.FullName;
            """;

        await using var connection = connections.Create();
        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { query.MatchId }, cancellationToken: cancellationToken));

        var match = await results.ReadSingleOrDefaultAsync<MatchDetailDto>();
        if (match is null)
        {
            return MatchErrors.NotFound(query.MatchId);
        }

        var goals = (await results.ReadAsync<GoalDto>()).AsList();

        return Result.Success(match with { Goals = goals });
    }
}
