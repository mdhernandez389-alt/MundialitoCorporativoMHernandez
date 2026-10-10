using System.Diagnostics;
using Mundialito.Api.Observability;
using Mundialito.Application;
using Mundialito.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Composition root (§2): la Api solo arma las piezas; la lógica vive en Application y Domain.
builder.AddObservability();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers(options =>
    // Sin esto, ASP.NET trata todo string no-nullable como [Required] y responde su propio 400
    // antes de llegar al dominio. Así la validación (y su código, p. ej. Team.NameRequired) es una sola.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Todo error sale como ProblemDetails, también los que genera ASP.NET (JSON inválido, ruta inexistente, 500),
// y todos llevan "code": el frontend siempre puede decidir por código.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd("code", context.ProblemDetails.Status switch
        {
            StatusCodes.Status400BadRequest => "Request.Invalid",
            StatusCodes.Status404NotFound => "Resource.NotFound",
            >= StatusCodes.Status500InternalServerError => "Server.Error",
            _ => "Request.Failed"
        });

        // Mismo TraceId que el header X-Trace-Id, los logs y Jaeger (ASP.NET pondría "00-<traceId>-<spanId>-01").
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.TraceId.ToHexString() ?? context.HttpContext.TraceIdentifier;
    });

builder.Services.AddOpenApi();

var app = builder.Build();

// Primero: CorrelationId/TraceId y log de duración envuelven todo lo demás, incluidos los errores.
app.UseObservability();
// Excepción no controlada (un bug, no un caso de negocio) → 500 ProblemDetails, sin stack trace al cliente.
app.UseExceptionHandler();
// Respuestas de error sin cuerpo (p. ej. 404 de una ruta inexistente) → ProblemDetails.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();
