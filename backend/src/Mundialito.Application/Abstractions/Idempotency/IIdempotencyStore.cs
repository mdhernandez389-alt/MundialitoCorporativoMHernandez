namespace Mundialito.Application.Abstractions.Idempotency;

/// <summary>
/// Almacén de las <c>Idempotency-Key</c> de los POST (§6): la key se persiste junto con la respuesta original,
/// así una repetición devuelve exactamente lo mismo sin volver a ejecutar el Command.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Reserva la key de forma atómica: si dos requests llegan a la vez con la misma key, solo uno la gana.
    /// Si ya existía, informa en qué estado está.
    /// </summary>
    /// <param name="requestHash">Huella del contenido del request (método + ruta + body).</param>
    Task<IdempotencyReservation> TryReserveAsync(string key, string requestHash, CancellationToken cancellationToken);

    /// <summary>Guarda la respuesta exitosa: desde ahora, repetir la key la devuelve tal cual.</summary>
    Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken);

    /// <summary>Libera una reserva cuyo request falló, para que el cliente pueda corregir y reintentar.</summary>
    Task ReleaseAsync(string key, CancellationToken cancellationToken);
}

public enum IdempotencyOutcome
{
    /// <summary>La key es nueva y este request la ganó: hay que ejecutar la acción.</summary>
    Reserved = 1,

    /// <summary>Otro request con la misma key se está ejecutando ahora mismo.</summary>
    InProgress = 2,

    /// <summary>La key ya se usó con este mismo contenido: se devuelve la respuesta guardada.</summary>
    Completed = 3,

    /// <summary>La key ya se usó, pero con OTRO contenido: es un error del cliente.</summary>
    KeyReused = 4
}

/// <param name="Response">Solo en <see cref="IdempotencyOutcome.Completed"/>: la respuesta original.</param>
public sealed record IdempotencyReservation(IdempotencyOutcome Outcome, IdempotentResponse? Response = null);

/// <summary>Lo necesario para repetir una respuesta HTTP: status, cuerpo JSON y header Location.</summary>
public sealed record IdempotentResponse(int StatusCode, string Body, string? Location);
