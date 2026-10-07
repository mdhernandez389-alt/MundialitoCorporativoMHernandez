namespace Mundialito.Application.Abstractions.Messaging;

/// <summary>
/// Operación de lectura (read side de CQRS). Se resuelve exclusivamente con Dapper y devuelve DTOs,
/// nunca entidades: no pasa por EF Core ni por el Unit of Work.
/// </summary>
public interface IQuery<TResponse>;
