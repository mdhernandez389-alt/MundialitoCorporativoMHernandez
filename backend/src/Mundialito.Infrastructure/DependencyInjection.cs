using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Matches;
using Mundialito.Application.Players;
using Mundialito.Application.Standings;
using Mundialito.Application.Teams;
using Mundialito.Infrastructure.Persistence;
using Mundialito.Infrastructure.Persistence.Repositories;

namespace Mundialito.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Mundialito";

    /// <summary>
    /// Registra la persistencia. Todo es <c>Scoped</c> (uno por request): el UnitOfWork y los repositorios
    /// reciben la MISMA instancia del DbContext, y por eso un commit confirma lo de todos los repositorios.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Falta la cadena de conexión '{ConnectionStringName}'.");

        services.AddDbContext<MundialitoDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<ITeamStandingRepository, TeamStandingRepository>();

        return services;
    }
}
