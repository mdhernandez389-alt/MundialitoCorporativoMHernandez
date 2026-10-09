using System.Reflection;
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

    /// <summary>Registra los Command handlers de Application.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services.AddHandlersFromAssembly(typeof(DependencyInjection).Assembly);

    /// <summary>
    /// Registra cada handler del ensamblado contra su interfaz (p. ej. <c>ICommandHandler&lt;CreateTeamCommand, Guid&gt;</c>).
    /// Un handler nuevo queda disponible sin tocar la DI. Infrastructure lo usa para sus Query handlers (Dapper).
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var registrations =
            from implementation in assembly.GetTypes()
            where implementation is { IsClass: true, IsAbstract: false }
            from service in implementation.GetInterfaces()
            where service.IsGenericType && HandlerInterfaces.Contains(service.GetGenericTypeDefinition())
            select (service, implementation);

        // Scoped: dependen de repositorios, UnitOfWork o conexiones, que viven uno por request.
        foreach (var (service, implementation) in registrations)
        {
            services.AddScoped(service, implementation);
        }

        return services;
    }
}
