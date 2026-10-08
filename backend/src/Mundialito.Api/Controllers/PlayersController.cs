using Microsoft.AspNetCore.Mvc;
using Mundialito.Api.Contracts;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Players.Commands.RegisterPlayer;

namespace Mundialito.Api.Controllers;

/// <summary>Jugadores como sub-recurso del equipo (§8: "registro por equipo").</summary>
[Route("api/teams/{teamId:guid}/players")]
public sealed class PlayersController : ApiController
{
    [HttpPost]
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
