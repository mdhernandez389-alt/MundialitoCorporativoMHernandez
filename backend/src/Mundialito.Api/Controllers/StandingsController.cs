using Microsoft.AspNetCore.Mvc;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Querying;
using Mundialito.Application.Standings.Queries;
using Mundialito.Application.Standings.Queries.GetStandings;

namespace Mundialito.Api.Controllers;

[Route("api/standings")]
public sealed class StandingsController : ApiController
{
    /// <summary>
    /// Tabla de posiciones. Query string: pageNumber, pageSize,
    /// sortBy (position|points|goalDifference|goalsFor|teamName), sortDirection, teamName, department.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<StandingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] GetStandingsQuery query,
        [FromServices] IQueryHandler<GetStandingsQuery, PagedResult<StandingDto>> handler,
        CancellationToken cancellationToken) =>
        OkOrProblem(await handler.HandleAsync(query, cancellationToken));
}
