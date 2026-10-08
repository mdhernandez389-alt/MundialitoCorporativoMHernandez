using Microsoft.AspNetCore.Mvc;
using Mundialito.Api.Contracts;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Teams.Commands.CreateTeam;
using Mundialito.Application.Teams.Commands.DeleteTeam;
using Mundialito.Application.Teams.Commands.PatchTeam;
using Mundialito.Application.Teams.Commands.UpdateTeam;

namespace Mundialito.Api.Controllers;

/// <summary>
/// Solo orquesta (§2): request → command → handler → HTTP. Cada acción recibe únicamente el handler que usa.
/// </summary>
[Route("api/teams")]
public sealed class TeamsController : ApiController
{
    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CreateTeamRequest request,
        [FromServices] ICommandHandler<CreateTeamCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CreateTeamCommand(request.Name, request.Department), cancellationToken);

        return result.IsSuccess
            ? Created($"/api/teams/{result.Value}", new CreatedResponse(result.Value))
            : Problem(result.Error);
    }

    [HttpPut("{teamId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid teamId,
        UpdateTeamRequest request,
        [FromServices] ICommandHandler<UpdateTeamCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new UpdateTeamCommand(teamId, request.Name, request.Department),
            cancellationToken);

        return NoContentOrProblem(result);
    }

    [HttpPatch("{teamId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Patch(
        Guid teamId,
        PatchTeamRequest request,
        [FromServices] ICommandHandler<PatchTeamCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new PatchTeamCommand(teamId, request.Name, request.Department),
            cancellationToken);

        return NoContentOrProblem(result);
    }

    [HttpDelete("{teamId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid teamId,
        [FromServices] ICommandHandler<DeleteTeamCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new DeleteTeamCommand(teamId), cancellationToken);

        return NoContentOrProblem(result);
    }
}
