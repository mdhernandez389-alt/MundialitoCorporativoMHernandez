using Mundialito.Application;
using Mundialito.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Composition root (§2): la Api solo arma las piezas; la lógica vive en Application y Domain.
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
        context.ProblemDetails.Extensions.TryAdd("code", context.ProblemDetails.Status switch
        {
            StatusCodes.Status400BadRequest => "Request.Invalid",
            StatusCodes.Status404NotFound => "Resource.NotFound",
            >= StatusCodes.Status500InternalServerError => "Server.Error",
            _ => "Request.Failed"
        }));

builder.Services.AddOpenApi();

var app = builder.Build();

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
