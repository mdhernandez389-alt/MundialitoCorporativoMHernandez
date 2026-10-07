namespace Mundialito.Application.Abstractions.Messaging;

/// <summary>
/// Operación de escritura (write side de CQRS) que no devuelve valor: PUT, PATCH, DELETE.
/// </summary>
/// <remarks>
/// Todo Command se resuelve con el dominio + EF Core y termina en <c>IUnitOfWork.CommitAsync</c>.
/// </remarks>
public interface ICommand;

/// <summary>
/// Operación de escritura que devuelve un valor, p. ej. el Id creado en un POST.
/// </summary>
public interface ICommand<TResponse>;
