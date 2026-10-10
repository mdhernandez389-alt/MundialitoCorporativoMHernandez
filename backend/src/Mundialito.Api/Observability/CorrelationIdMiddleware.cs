using System.Diagnostics;
using Mundialito.Application.Abstractions.Observability;

namespace Mundialito.Api.Observability;

/// <summary>CorrelationId del request en curso. Lo escribe <see cref="CorrelationIdMiddleware"/>.</summary>
internal sealed class CorrelationContext : ICorrelationContext
{
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Trazabilidad por request (§7):
/// <list type="bullet">
/// <item><b>CorrelationId</b>: el que manda el cliente en <c>X-Correlation-Id</c> (puede agrupar varios requests
/// de una misma acción) o uno nuevo. Se devuelve en la respuesta.</item>
/// <item><b>TraceId</b>: el W3C de este request (el de Jaeger y los ProblemDetails). Se devuelve en <c>X-Trace-Id</c>.</item>
/// </list>
/// Todo log escrito durante el request lleva los dos: así se encuentran todos los logs de un request a partir
/// de cualquiera de ellos.
/// </summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string CorrelationHeader = "X-Correlation-Id";
    public const string TraceHeader = "X-Trace-Id";
    private const int MaxLength = 100;

    public async Task InvokeAsync(HttpContext context, CorrelationContext correlation)
    {
        var correlationId = ReadOrCreateCorrelationId(context.Request);
        var traceId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;

        correlation.CorrelationId = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        // OnStarting: los headers se agregan justo antes de enviar la respuesta, también en las de error.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeader] = correlationId;
            context.Response.Headers[TraceHeader] = traceId;
            return Task.CompletedTask;
        });

        // BeginScope (de ILogger, no de Serilog): el middleware no depende del proveedor de logs.
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    /// <summary>
    /// El valor del cliente termina en logs y en headers de respuesta: solo se acepta si es corto y con
    /// caracteres seguros. Así nadie puede inyectar saltos de línea en los logs ni headers falsos.
    /// </summary>
    private static string ReadOrCreateCorrelationId(HttpRequest request)
    {
        var value = request.Headers[CorrelationHeader].ToString();

        var isSafe = value.Length is > 0 and <= MaxLength
            && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

        return isSafe ? value : Guid.NewGuid().ToString("N");
    }
}
