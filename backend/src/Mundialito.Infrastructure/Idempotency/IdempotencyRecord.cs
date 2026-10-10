namespace Mundialito.Infrastructure.Idempotency;

/// <summary>
/// Fila de <c>IdempotencyRecords</c>. EF Core solo la usa para crear la tabla en la migración;
/// la lectura y escritura las hace <see cref="SqlIdempotencyStore"/> con SQL (ver por qué ahí).
/// </summary>
internal sealed class IdempotencyRecord
{
    public const int KeyMaxLength = 100;
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";

    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>SHA-256 en hexadecimal de método + ruta + body.</summary>
    public string RequestHash { get; init; } = string.Empty;

    public string Status { get; init; } = InProgress;

    public int? ResponseStatusCode { get; init; }

    public string? ResponseBody { get; init; }

    public string? ResponseLocation { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }
}
