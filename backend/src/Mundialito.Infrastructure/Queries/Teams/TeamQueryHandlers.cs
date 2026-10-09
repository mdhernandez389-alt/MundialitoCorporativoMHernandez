using Dapper;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;
using Mundialito.Application.Teams.Queries;
using Mundialito.Application.Teams.Queries.GetTeamById;
using Mundialito.Application.Teams.Queries.GetTeams;
using Mundialito.Domain.Results;
using Mundialito.Domain.Teams;
using Mundialito.Infrastructure.Queries.Common;

namespace Mundialito.Infrastructure.Queries.Teams;

internal sealed class GetTeamsHandler(DbConnectionFactory connections)
    : IQueryHandler<GetTeamsQuery, PagedResult<TeamDto>>
{
    private static readonly SortMap Sorting = new(
        defaultSortBy: "name",
        tieBreaker: "t.Id",
        new Dictionary<string, string>
        {
            ["name"] = "t.Name",
            ["department"] = "t.Department",
        });

    public async Task<Result<PagedResult<TeamDto>>> HandleAsync(GetTeamsQuery query, CancellationToken cancellationToken)
    {
        var where = new SqlWhere()
            .AddContains("t.Name", "Name", query.Name)
            .AddContains("t.Department", "Department", query.Department);

        await using var connection = connections.Create();

        return await PagedSql.QueryAsync<TeamDto>(
            connection,
            query,
            Sorting,
            select: "SELECT t.Id, t.Name, t.Department",
            from: "FROM Teams t",
            where,
            cancellationToken);
    }
}

internal sealed class GetTeamByIdHandler(DbConnectionFactory connections)
    : IQueryHandler<GetTeamByIdQuery, TeamDto>
{
    public async Task<Result<TeamDto>> HandleAsync(GetTeamByIdQuery query, CancellationToken cancellationToken)
    {
        const string sql = "SELECT t.Id, t.Name, t.Department FROM Teams t WHERE t.Id = @TeamId";

        await using var connection = connections.Create();
        var team = await connection.QuerySingleOrDefaultAsync<TeamDto>(
            new CommandDefinition(sql, new { query.TeamId }, cancellationToken: cancellationToken));

        return team is null ? TeamErrors.NotFound(query.TeamId) : Result.Success(team);
    }
}
