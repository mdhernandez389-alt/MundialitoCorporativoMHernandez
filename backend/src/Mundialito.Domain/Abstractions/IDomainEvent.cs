namespace Mundialito.Domain.Abstractions;

/// <summary>
/// Algo relevante que ya ocurrió en el negocio (se nombra en pasado: TeamCreated, MatchResultRegistered).
/// </summary>
public interface IDomainEvent
{
    /// <summary>Identifica el evento de forma única (útil para registrarlo y rastrearlo en los logs).</summary>
    Guid EventId { get; }

    DateTime OccurredOnUtc { get; }
}
