using Microsoft.AspNetCore.Mvc;
using Mundialito.Api.Contracts;
using Mundialito.Api.Idempotency;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;
using Mundialito.Application.Teams.Commands.CreateTeam;
using Mundialito.Application.Teams.Commands.DeleteTeam;
using Mundialito.Application.Teams.Commands.PatchTeam;
using Mundialito.Application.Teams.Commands.UpdateTeam;
using Mundialito.Application.Teams.Queries;
using Mundialito.Application.Teams.Queries.GetTeamById;
using Mundialito.Application.Teams.Queries.GetTeams;

namespace Mundialito.Api.Controllers;

/// <summary>
/// Solo orquesta (§2): request → command → handler → HTTP. Cada acción recibe únicamente el handler que usa.
/// </summary>
[Route("api/teams")]
public sealed class TeamsController : ApiController
{
    /// <summary>Lista paginada. Query string: pageNumber, pageSize, sortBy (name|department), sortDirection, name, department.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<TeamDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetTeamsQuery query,
        [FromServices] IQueryHandler<GetTeamsQuery, PagedResult<TeamDto>> handler,
        CancellationToken cancellationToken) =>
        OkOrProblem(await handler.HandleAsync(query, cancellationToken));

    [HttpGet("{teamId:guid}")]
    [ProducesResponseType<TeamDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid teamId,
        [FromServices] IQueryHandler<GetTeamByIdQuery, TeamDto> handler,
        CancellationToken cancellationToken) =>
        OkOrProblem(await handler.HandleAsync(new GetTeamByIdQuery(teamId), cancellationToken));

    [HttpPost]
    [Idempotent]
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
