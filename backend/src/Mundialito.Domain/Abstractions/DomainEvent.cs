namespace Mundialito.Domain.Abstractions;

/// <summary>
/// Base de los eventos de dominio: cada evento concreto solo declara sus datos de negocio.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
