using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Mundialito.Application.Abstractions.Idempotency;
using JsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace Mundialito.Api.Idempotency;

/// <summary>
/// Idempotencia de los POST (§6), siguiendo el borrador IETF <c>draft-ietf-httpapi-idempotency-key-header</c>:
/// <list type="bullet">
/// <item>Sin <c>Idempotency-Key</c> → 400.</item>
/// <item>Key nueva → se ejecuta la acción y, si sale bien, se guarda la respuesta.</item>
/// <item>Key repetida con el mismo contenido → la respuesta original, sin volver a ejecutar.</item>
/// <item>Key repetida con otro contenido → 422.</item>
/// <item>Key de un request que todavía se está ejecutando → 409.</item>
/// </list>
/// </summary>
internal sealed class IdempotencyFilter(
    IIdempotencyStore store,
    IOptions<JsonOptions> jsonOptions,
    ILogger<IdempotencyFilter> logger) : IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";
    private const int KeyMaxLength = 100;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var key = httpContext.Request.Headers[HeaderName].ToString().Trim();

        if (key.Length == 0)
        {
            context.Result = Problem(httpContext, StatusCodes.Status400BadRequest,
                "Idempotency.KeyRequired", $"Falta el header {HeaderName}: todo POST debe enviarlo.");
            return;
        }

        if (key.Length > KeyMaxLength)
        {
            context.Result = Problem(httpContext, StatusCodes.Status400BadRequest,
                "Idempotency.KeyTooLong", $"{HeaderName} no puede superar {KeyMaxLength} caracteres.");
            return;
        }

        var reservation = await store.TryReserveAsync(key, ComputeRequestHash(context), httpContext.RequestAborted);

        // Solo una key recién reservada ejecuta la acción. Los demás casos asignan context.Result, y eso
        // corta la ejecución: el controller no corre y no se crea nada.
        if (reservation.Outcome == IdempotencyOutcome.Reserved)
        {
            await ExecuteAndStoreAsync(context, next, key);
            return;
        }

        if (reservation.Outcome == IdempotencyOutcome.Completed)
        {
            context.Result = Replay(httpContext, reservation.Response!);
            return;
        }

        if (reservation.Outcome == IdempotencyOutcome.KeyReused)
        {
            context.Result = Problem(httpContext, StatusCodes.Status422UnprocessableEntity, "Idempotency.KeyReused",
                "Idempotency-Key ya utilizada, usa una key nueva para una petición distinta.");
            return;
        }

        if (reservation.Outcome == IdempotencyOutcome.InProgress)
        {
            context.Result = Problem(httpContext, StatusCodes.Status409Conflict, "Idempotency.RequestInProgress",
                "Petición en proceso para esta Idempotency-Key. Reintenta en unos segundos.");
            return;
        }

        // Sin esto, un Outcome nuevo dejaría pasar el request sin ejecutar nada y respondería 200 vacío.
        throw new ArgumentOutOfRangeException(nameof(reservation), reservation.Outcome, "Resultado de reserva no contemplado.");
    }

    /// <summary>
    /// Ejecuta la acción. Si sale bien (2xx), guarda su respuesta; si no, libera la reserva para que el cliente
    /// pueda corregir y reintentar con la misma key.
    /// </summary>
    private async Task ExecuteAndStoreAsync(ActionExecutingContext context, ActionExecutionDelegate next, string key)
    {
        ActionExecutedContext executed;
        try
        {
            executed = await next();
        }
        catch
        {
            await store.ReleaseAsync(key, CancellationToken.None);
            throw;
        }

        // CancellationToken.None: si el Command ya se confirmó, la respuesta se guarda aunque el cliente
        // haya cortado la conexión; justo ese cliente es el que va a reintentar.
        if (executed.Exception is null && executed.Result is ObjectResult { StatusCode: >= 200 and < 300 } result)
        {
            await store.CompleteAsync(key, ToIdempotentResponse(result), CancellationToken.None);
            return;
        }

        // Error de negocio (400/404/409) o excepción: no se guarda nada; reintentar vuelve a ejecutar.
        await store.ReleaseAsync(key, CancellationToken.None);
    }

    /// <summary>Devuelve la respuesta guardada tal cual: mismo status, mismo cuerpo, mismo Location.</summary>
    private IActionResult Replay(HttpContext httpContext, IdempotentResponse response)
    {
        logger.LogInformation(
            "Respuesta idempotente repetida para {Method} {Path} (status {StatusCode})",
            httpContext.Request.Method,
            httpContext.Request.Path,
            response.StatusCode);

        if (response.Location is not null)
        {
            httpContext.Response.Headers.Location = response.Location;
        }

        // Le avisa al cliente que no se ejecutó nada nuevo.
        httpContext.Response.Headers[ReplayedHeaderName] = "true";

        return new ContentResult
        {
            StatusCode = response.StatusCode,
            Content = response.Body,
            ContentType = "application/json; charset=utf-8",
        };
    }

    private IdempotentResponse ToIdempotentResponse(ObjectResult result)
    {
        // Mismas opciones JSON que usa MVC: el cuerpo repetido es idéntico byte a byte al original.
        var body = JsonSerializer.Serialize(
            result.Value,
            result.DeclaredType ?? result.Value?.GetType() ?? typeof(object),
            jsonOptions.Value.JsonSerializerOptions);

        return new IdempotentResponse(
            result.StatusCode ?? StatusCodes.Status200OK,
            body,
            (result as CreatedResult)?.Location);
    }

    /// <summary>
    /// Huella del request: método + ruta + body ya parseado (así un JSON con otros espacios o con los campos en
    /// otro orden cuenta como el mismo contenido). La ruta incluye los Ids, p. ej. <c>/api/teams/{id}/players</c>.
    /// </summary>
    private string ComputeRequestHash(ActionExecutingContext context)
    {
        var bodies = context.ActionDescriptor.Parameters
            .Where(p => p.BindingInfo?.BindingSource == BindingSource.Body)
            .Select(p => context.ActionArguments.TryGetValue(p.Name, out var value) ? value : null);

        var fingerprint = string.Join('\n',
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path.Value,
            JsonSerializer.Serialize(bodies, jsonOptions.Value.JsonSerializerOptions));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
    }

    /// <summary>Mismo formato de error que el resto de la Api: ProblemDetails + <c>code</c> + traceId.</summary>
    private static ObjectResult Problem(HttpContext httpContext, int statusCode, string code, string detail)
    {
        var factory = httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateProblemDetails(httpContext, statusCode: statusCode, detail: detail);
        problem.Extensions["code"] = code;

        return new ObjectResult(problem) { StatusCode = statusCode };
    }
}
