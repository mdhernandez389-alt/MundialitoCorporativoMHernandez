using Mundialito.Domain.Results;

namespace Mundialito.Application.Abstractions.Messaging;

/// <summary>
/// Ejecuta un <see cref="ICommand"/>. Los fallos de negocio viajan en el <see cref="Result"/>, nunca como excepción.
/// </summary>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Ejecuta un <see cref="ICommand{TResponse}"/> y devuelve su valor si fue exitoso.
/// </summary>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
