namespace Mundialito.Domain.Abstractions;

/// <summary>
/// Base de las entidades: identidad y eventos de dominio pendientes de publicar.
/// </summary>
/// <remarks>
/// La entidad solo acumula sus eventos; quién los registra, loggea y despacha (y cuándo) lo decide
/// la infraestructura al hacer commit en el Unit of Work.
/// </remarks>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected Entity(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
