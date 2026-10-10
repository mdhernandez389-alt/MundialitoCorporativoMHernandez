using Mundialito.Application.Abstractions.Observability;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace Mundialito.Api.Observability;

/// <summary>
/// Observabilidad (§7): logs estructurados con Serilog y trazas con OpenTelemetry.
/// El código de la aplicación solo usa <c>ILogger&lt;T&gt;</c>: Serilog es el motor, no una dependencia de los handlers.
/// </summary>
internal static class ObservabilityExtensions
{
    public const string ServiceName = "Mundialito.Api";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<CorrelationContext>();
        builder.Services.AddScoped<ICorrelationContext>(services => services.GetRequiredService<CorrelationContext>());

        // Logs: niveles, salida (consola / Seq) y formato vienen de la sección "Serilog" de appsettings.
        // FromLogContext incorpora el CorrelationId del middleware; el TraceId lo toma Serilog de la traza activa.
        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", ServiceName));

        // Trazas: cada request y cada consulta SQL (EF Core y Dapper usan el mismo Microsoft.Data.SqlClient).
        // Sin "OpenTelemetry:OtlpEndpoint" igual se generan TraceIds (para logs y respuestas), solo no se exportan.
        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddSqlClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return builder;
    }

    /// <summary>
    /// Va primero en el pipeline: así TODO lo que pasa después (incluido el manejo de excepciones)
    /// queda dentro del CorrelationId y del log de duración.
    /// </summary>
    public static WebApplication UseObservability(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();

        // Una línea por request con método, ruta, status y duración (§7: "registro de duración de requests").
        // No incluye body, query string ni headers: nada sensible.
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} respondió {StatusCode} en {Elapsed:0.0} ms";
            options.GetLevel = GetRequestLogLevel;
        });

        return app;
    }

    /// <summary>Nivel del log de cada request según cómo terminó (§7: "niveles correctos").</summary>
    private static LogEventLevel GetRequestLogLevel(HttpContext httpContext, double elapsedMs, Exception? exception)
    {
        // Primero la excepción: si nadie alcanzó a escribir el 500, el StatusCode todavía puede decir 200.
        if (exception is not null)
        {
            return LogEventLevel.Error;
        }

        // Error = algo NUESTRO falló (hay que actuar). Warning = el cliente pidió algo inválido (respuesta correcta,
        // pero vale verlo si se dispara). Así filtrar "Error" en Seq muestra solo problemas reales.
        return httpContext.Response.StatusCode switch
        {
            >= 500 => LogEventLevel.Error,
            >= 400 => LogEventLevel.Warning,
            _ => LogEventLevel.Information,
        };
    }
}
