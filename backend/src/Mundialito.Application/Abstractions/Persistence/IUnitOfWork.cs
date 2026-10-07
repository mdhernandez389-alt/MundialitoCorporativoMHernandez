using Mundialito.Domain.Results;

namespace Mundialito.Application.Abstractions.Persistence;

/// <summary>
/// Persiste los cambios, los mete en un trasacción y devuelve el resultado, si falló siempre es un result y no una excepcción
/// </summary>
/// <remarks>
/// Los repositorios se inyectan por separado y comparten con el Unit of Work el mismo <c>DbContext</c>
/// (scoped por request): todo lo que rastrearon se confirma junto, en una sola transacción.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Confirma en una sola transacción todos los cambios pendientes. Si choca con otro request
    /// (nombre duplicado, concurrencia) devuelve un <see cref="ErrorType.Conflict"/> en vez de lanzar excepción.
    /// </summary>
    Task<Result> CommitAsync(CancellationToken cancellationToken);
}
