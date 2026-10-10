using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mundialito.Api.Idempotency;
using Mundialito.Application.Abstractions.Idempotency;

namespace Mundialito.UnitTests.Api;

/// <summary>
/// Idempotencia de los POST (§6, §15): qué hace el filtro según el estado de la key.
/// El almacén es falso (en memoria): aquí se prueba la DECISIÓN; la reserva atómica en SQL se prueba en integración.
/// </summary>
public sealed class IdempotencyFilterTests
{
    private const string Key = "7f3c2a10-key";
    private static readonly object Body = new { name = "Sistemas", department = "TI" };

    private readonly FakeIdempotencyStore _store = new();
    private readonly IServiceProvider _services = new ServiceCollection().AddLogging().AddControllers().Services.BuildServiceProvider();

    [Fact]
    public async Task Sin_key_responde_400_y_no_ejecuta_la_accion()
    {
        var (context, next) = CreateContext(key: null, actionResult: Created());

        await CreateFilter().OnActionExecutionAsync(context, next.Delegate);

        Assert.Equal(StatusCodes.Status400BadRequest, StatusOf(context.Result));
        Assert.False(next.Executed);
    }

    [Fact]
    public async Task Una_key_nueva_ejecuta_la_accion_y_guarda_su_respuesta()
    {
        _store.Next = new IdempotencyReservation(IdempotencyOutcome.Reserved);
        var (context, next) = CreateContext(Key, Created());

        await CreateFilter().OnActionExecutionAsync(context, next.Delegate);

        Assert.True(next.Executed);
        var stored = Assert.Single(_store.Completed);
        Assert.Equal(StatusCodes.Status201Created, stored.StatusCode);
        Assert.Equal("/api/teams/42", stored.Location);
        Assert.Contains("42", stored.Body);
    }

    [Fact]
    public async Task Una_key_completada_repite_la_respuesta_original_sin_ejecutar_la_accion()
    {
        _store.Next = new IdempotencyReservation(
            IdempotencyOutcome.Completed,
            new IdempotentResponse(StatusCodes.Status201Created, """{"id":"42"}""", "/api/teams/42"));
        var (context, next) = CreateContext(Key, Created());

        await CreateFilter().OnActionExecutionAsync(context, next.Delegate);

        Assert.False(next.Executed);
        var replay = Assert.IsType<ContentResult>(context.Result);
        Assert.Equal(StatusCodes.Status201Created, replay.StatusCode);
        Assert.Equal("""{"id":"42"}""", replay.Content);
        Assert.Equal("/api/teams/42", context.HttpContext.Response.Headers.Location.ToString());
        Assert.Equal("true", context.HttpContext.Response.Headers[IdempotencyFilter.ReplayedHeaderName].ToString());
    }

    [Fact]
    public async Task Una_key_reusada_con_otro_contenido_responde_422()
    {
        _store.Next = new IdempotencyReservation(IdempotencyOutcome.KeyReused);
        var (context, next) = CreateContext(Key, Created());

        await CreateFilter().OnActionExecutionAsync(context, next.Delegate);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, StatusOf(context.Result));
        Assert.False(next.Executed);
    }

    [Fact]
    public async Task Una_key_en_curso_responde_409()
    {
        _store.Next = new IdempotencyReservation(IdempotencyOutcome.InProgress);
        var (context, next) = CreateContext(Key, Created());

        await CreateFilter().OnActionExecutionAsync(context, next.Delegate);

        Assert.Equal(StatusCodes.Status409Conflict, StatusOf(context.Result));
        Assert.False(next.Executed);
    }

    [Fact]
    public async Task Si_la_accion_falla_se_libera_la_key_para_poder_corregir_y_reintentar()
    {
        _store.Next = new IdempotencyReservation(IdempotencyOutcome.Reserved);
        var businessConflict = new ObjectResult(new ProblemDetails()) { StatusCode = StatusCodes.Status409Conflict };
        var (context, next) = CreateContext(Key, businessConflict);

        await CreateFilter().OnActionExecutionAsync(context, next.Delegate);

        Assert.Empty(_store.Completed);
        Assert.Equal([Key], _store.Released);
    }

    [Fact]
    public async Task El_mismo_contenido_produce_la_misma_huella_y_otro_contenido_otra()
    {
        _store.Next = new IdempotencyReservation(IdempotencyOutcome.InProgress);

        await CreateFilter().OnActionExecutionAsync(CreateContext(Key, Created(), new { name = "A" }).Context, () => null!);
        await CreateFilter().OnActionExecutionAsync(CreateContext(Key, Created(), new { name = "A" }).Context, () => null!);
        await CreateFilter().OnActionExecutionAsync(CreateContext(Key, Created(), new { name = "B" }).Context, () => null!);

        Assert.Equal(_store.Hashes[0], _store.Hashes[1]);
        Assert.NotEqual(_store.Hashes[0], _store.Hashes[2]);
    }

    private IdempotencyFilter CreateFilter() =>
        new(_store, Options.Create(new JsonOptions()), NullLogger<IdempotencyFilter>.Instance);

    private static CreatedResult Created() => new("/api/teams/42", new { id = "42" });

    private static int? StatusOf(IActionResult? result) => Assert.IsAssignableFrom<ObjectResult>(result).StatusCode;

    /// <summary>Arma lo que MVC le pasa al filtro: un POST con su body ya parseado y la acción que "ejecutaría".</summary>
    private (ActionExecutingContext Context, NextAction Next) CreateContext(string? key, IActionResult actionResult, object? body = null)
    {
        var httpContext = new DefaultHttpContext { RequestServices = _services };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/teams";
        if (key is not null)
        {
            httpContext.Request.Headers[IdempotencyFilter.HeaderName] = key;
        }

        var descriptor = new ActionDescriptor
        {
            Parameters =
            [
                new ParameterDescriptor
                {
                    Name = "request",
                    ParameterType = typeof(object),
                    BindingInfo = new BindingInfo { BindingSource = BindingSource.Body },
                },
            ],
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);
        var arguments = new Dictionary<string, object?> { ["request"] = body ?? Body };
        var context = new ActionExecutingContext(actionContext, [], arguments, controller: new object());

        return (context, new NextAction(actionContext, actionResult));
    }

    /// <summary>La "acción del controller": registra si se ejecutó y devuelve el resultado indicado.</summary>
    private sealed class NextAction(ActionContext actionContext, IActionResult result)
    {
        public bool Executed { get; private set; }

        public ActionExecutionDelegate Delegate => () =>
        {
            Executed = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()) { Result = result });
        };
    }

    private sealed class FakeIdempotencyStore : IIdempotencyStore
    {
        public IdempotencyReservation Next { get; set; } = new(IdempotencyOutcome.Reserved);

        public List<string> Hashes { get; } = [];

        public List<IdempotentResponse> Completed { get; } = [];

        public List<string> Released { get; } = [];

        public Task<IdempotencyReservation> TryReserveAsync(string key, string requestHash, CancellationToken cancellationToken)
        {
            Hashes.Add(requestHash);
            return Task.FromResult(Next);
        }

        public Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken)
        {
            Completed.Add(response);
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(string key, CancellationToken cancellationToken)
        {
            Released.Add(key);
            return Task.CompletedTask;
        }
    }
}
