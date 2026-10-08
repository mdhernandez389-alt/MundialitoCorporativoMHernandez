using Microsoft.AspNetCore.Mvc;
using Mundialito.Domain.Results;

namespace Mundialito.Api.Controllers;

/// <summary>
/// Base de los controllers: único lugar donde un <see cref="Error"/> de negocio se traduce a HTTP (§5).
/// </summary>
[ApiController]
public abstract class ApiController : ControllerBase
{
    /// <summary>
    /// Respuesta de error en formato ProblemDetails (RFC 9457) + <c>code</c>, el mismo formato que ASP.NET
    /// usa para JSON inválido y para los 500: el frontend maneja uno solo.
    /// </summary>
    protected ObjectResult Problem(Error error)
    {
        // La factory de ASP.NET agrega type, title y traceId (para cruzar con los logs, §7).
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            statusCode: StatusCodeFor(error.Type),
            detail: error.Message);

        // El código estable es el contrato con el cliente: decide por "Team.NameDuplicated", no por el texto.
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem) { StatusCode = problem.Status };
    }

    /// <summary>204 si salió bien; ProblemDetails si no. Para PUT, PATCH y DELETE (Commands sin valor).</summary>
    protected IActionResult NoContentOrProblem(Result result) =>
        result.IsSuccess ? NoContent() : Problem(result.Error);

    private static int StatusCodeFor(ErrorType type)
    {
        return type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            // None (Problem() con un resultado exitoso) o un tipo nuevo sin traducir: es un bug.
            // La excepción llega al UseExceptionHandler → 500 al cliente y queda loggeada con stack trace.
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Este tipo de error no tiene status HTTP.")
        };
    }
}
