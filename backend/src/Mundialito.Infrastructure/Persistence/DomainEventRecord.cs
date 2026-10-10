using System.Text.Json;
using Mundialito.Domain.Abstractions;

namespace Mundialito.Infrastructure.Persistence;

/// <summary>
/// Fila de la tabla <c>DomainEvents</c>: constancia de que un evento ocurrió (§7).
/// Se guarda en la misma transacción que el cambio que lo produjo, así existe solo si el cambio se guardó.
/// </summary>
internal sealed class DomainEventRecord
{
    public const int TypeMaxLength = 200;

    public required Guid Id { get; init; }

    /// <summary>Nombre del evento, p. ej. <c>TeamCreatedDomainEvent</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Datos del evento en JSON.</summary>
    public required string Payload { get; init; }

    public required DateTime OccurredOnUtc { get; init; }

    /// <summary>TraceId W3C del request que generó el evento: el mismo de los logs y de Jaeger.</summary>
    public string? TraceId { get; init; }

    /// <summary>CorrelationId del request (el que envió el cliente o uno generado).</summary>
    public string? CorrelationId { get; init; }

    public static DomainEventRecord From(IDomainEvent domainEvent, string? traceId, string? correlationId) => new()
    {
        Id = domainEvent.EventId,
        Type = domainEvent.GetType().Name,
        // GetType() para serializar el evento concreto (con sus datos), no solo la interfaz.
        Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
        OccurredOnUtc = domainEvent.OccurredOnUtc,
        TraceId = traceId,
        CorrelationId = correlationId
    };
}
