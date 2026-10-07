using Mundialito.Domain.Results;

namespace Mundialito.Application.Abstractions.Messaging;

/// <summary>
/// Ejecuta un <see cref="IQuery{TResponse}"/>. Devuelve <see cref="Result{TValue}"/> porque una lectura
/// también puede fallar de forma esperada (p. ej. <c>GET /teams/{id}</c> inexistente → 404).
/// </summary>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
