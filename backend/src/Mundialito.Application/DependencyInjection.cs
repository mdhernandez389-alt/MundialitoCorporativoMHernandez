using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application;

public static class DependencyInjection
{
    private static readonly Type[] HandlerInterfaces =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>)
    ];

    /// <summary>
    /// Registra cada handler de este proyecto contra su interfaz (p. ej. <c>ICommandHandler&lt;CreateTeamCommand, Guid&gt;</c>).
    /// Un handler nuevo queda disponible sin tocar este archivo.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var registrations =
            from implementation in typeof(DependencyInjection).Assembly.GetTypes()
            where implementation is { IsClass: true, IsAbstract: false }
            from service in implementation.GetInterfaces()
            where service.IsGenericType && HandlerInterfaces.Contains(service.GetGenericTypeDefinition())
            select (service, implementation);

        // Scoped: dependen de los repositorios y del UnitOfWork, que viven uno por request.
        foreach (var (service, implementation) in registrations)
        {
            services.AddScoped(service, implementation);
        }

        return services;
    }
}
