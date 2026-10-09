using Microsoft.AspNetCore.Mvc;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;
using Mundialito.Application.Scorers.Queries;
using Mundialito.Application.Scorers.Queries.GetScorers;

namespace Mundialito.Api.Controllers;

[Route("api/scorers")]
public sealed class ScorersController : ApiController
{
    /// <summary>
    /// Tabla de goleadores. Query string: pageNumber, pageSize,
    /// sortBy (position|goals|playerName|teamName), sortDirection, teamId, playerName.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ScorerDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] GetScorersQuery query,
        [FromServices] IQueryHandler<GetScorersQuery, PagedResult<ScorerDto>> handler,
        CancellationToken cancellationToken) =>
        OkOrProblem(await handler.HandleAsync(query, cancellationToken));
}
