using Mundialito.Domain.Abstractions;

namespace Mundialito.Domain.Teams;

public sealed record TeamCreatedDomainEvent(Guid TeamId, string Name) : DomainEvent;
