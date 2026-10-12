using Microsoft.AspNetCore.Mvc;
using Mundialito.Api.Contracts;
using Mundialito.Api.Idempotency;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;
using Mundialito.Application.Matches.Commands.RegisterMatchResult;
using Mundialito.Application.Matches.Commands.ScheduleMatch;
using Mundialito.Application.Matches.Queries;
using Mundialito.Application.Matches.Queries.GetMatchById;
using Mundialito.Application.Matches.Queries.GetMatches;

namespace Mundialito.Api.Controllers;

[Route("api/matches")]
public sealed class MatchesController : ApiController
{
    /// <summary>
    /// Lista paginada. Query string: pageNumber, pageSize, sortBy (scheduledAt|status), sortDirection,
    /// from, to (fechas con zona; el "+" va como %2B), teamId, status (Scheduled|Played).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<MatchDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetMatchesQuery query,
        [FromServices] IQueryHandler<GetMatchesQuery, PagedResult<MatchDto>> handler,
        CancellationToken cancellationToken) =>
        OkOrProblem(await handler.HandleAsync(query, cancellationToken));

    [HttpGet("{matchId:guid}")]
    [ProducesResponseType<MatchDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid matchId,
        [FromServices] IQueryHandler<GetMatchByIdQuery, MatchDetailDto> handler,
        CancellationToken cancellationToken) =>
        OkOrProblem(await handler.HandleAsync(new GetMatchByIdQuery(matchId), cancellationToken));

    [HttpPost]
    [Idempotent]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Schedule(
        ScheduleMatchRequest request,
        [FromServices] ICommandHandler<ScheduleMatchCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        // [Required] ya garantizó que no son null. La fecha llega con zona horaria y se normaliza a UTC.
        var command = new ScheduleMatchCommand(
            request.HomeTeamId!.Value,
            request.AwayTeamId!.Value,
            request.ScheduledAt!.Value.UtcDateTime);

        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? Created($"/api/matches/{result.Value}", new CreatedResponse(result.Value))
            : Problem(result.Error);
    }

    /// <summary>
    /// El resultado es un sub-recurso que se reemplaza completo: PUT. Repetirlo con los mismos goles
    /// no cambia nada (ni la tabla ni los eventos).
    /// </summary>
    [HttpPut("{matchId:guid}/result")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterResult(
        Guid matchId,
        RegisterMatchResultRequest request,
        [FromServices] ICommandHandler<RegisterMatchResultCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RegisterMatchResultCommand(matchId, request.ScorerPlayerIds!, request.OwnGoalPlayerIds ?? []),
            cancellationToken);

        return NoContentOrProblem(result);
    }
}
