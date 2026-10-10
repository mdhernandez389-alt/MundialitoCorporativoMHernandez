using Microsoft.AspNetCore.Mvc;
using Mundialito.Api.Contracts;
using Mundialito.Api.Idempotency;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;
using Mundialito.Application.Players.Commands.RegisterPlayer;
using Mundialito.Application.Players.Queries;
using Mundialito.Application.Players.Queries.GetPlayerById;
using Mundialito.Application.Players.Queries.GetTeamPlayers;

namespace Mundialito.Api.Controllers;

/// <summary>Jugadores como sub-recurso del equipo (§8: "registro por equipo").</summary>
[Route("api/teams/{teamId:guid}/players")]
public sealed class PlayersController : ApiController
{
    /// <summary>Lista paginada del equipo. Query string: pageNumber, pageSize, sortBy (shirtNumber|fullName), sortDirection, name.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PlayerDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(
        Guid teamId,
        [FromQuery] GetTeamPlayersQuery query,
        [FromServices] IQueryHandler<GetTeamPlayersQuery, PagedResult<PlayerDto>> handler,
        CancellationToken cancellationToken) =>
        // El equipo sale de la ruta, aunque alguien mande ?teamId=... en el query string.
        OkOrProblem(await handler.HandleAsync(query with { TeamId = teamId }, cancellationToken));

    [HttpGet("{playerId:guid}")]
    [ProducesResponseType<PlayerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid teamId,
        Guid playerId,
        [FromServices] IQueryHandler<GetPlayerByIdQuery, PlayerDto> handler,
        CancellationToken cancellationToken) =>
        OkOrProblem(await handler.HandleAsync(new GetPlayerByIdQuery(teamId, playerId), cancellationToken));

    [HttpPost]
    [Idempotent]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        Guid teamId,
        RegisterPlayerRequest request,
        [FromServices] ICommandHandler<RegisterPlayerCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RegisterPlayerCommand(teamId, request.FullName, request.ShirtNumber),
            cancellationToken);

        return result.IsSuccess
            ? Created($"/api/teams/{teamId}/players/{result.Value}", new CreatedResponse(result.Value))
            : Problem(result.Error);
    }
}
