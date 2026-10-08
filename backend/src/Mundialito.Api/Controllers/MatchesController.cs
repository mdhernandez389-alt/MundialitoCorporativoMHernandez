using Microsoft.AspNetCore.Mvc;
using Mundialito.Api.Contracts;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Matches.Commands.RegisterMatchResult;
using Mundialito.Application.Matches.Commands.ScheduleMatch;

namespace Mundialito.Api.Controllers;

[Route("api/matches")]
public sealed class MatchesController : ApiController
{
    [HttpPost]
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
            new RegisterMatchResultCommand(matchId, request.ScorerPlayerIds!),
            cancellationToken);

        return NoContentOrProblem(result);
    }
}
