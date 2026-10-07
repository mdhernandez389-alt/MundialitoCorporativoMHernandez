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

    public static DomainEventRecord From(IDomainEvent domainEvent) => new()
    {
        Id = domainEvent.EventId,
        Type = domainEvent.GetType().Name,
        // GetType() para serializar el evento concreto (con sus datos), no solo la interfaz.
        Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
        OccurredOnUtc = domainEvent.OccurredOnUtc
    };
}
