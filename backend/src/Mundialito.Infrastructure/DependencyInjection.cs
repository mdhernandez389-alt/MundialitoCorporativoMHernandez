using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application;
using Mundialito.Application.Abstractions.Idempotency;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Matches;
using Mundialito.Application.Players;
using Mundialito.Application.Standings;
using Mundialito.Application.Teams;
using Mundialito.Infrastructure.Idempotency;
using Mundialito.Infrastructure.Persistence;
using Mundialito.Infrastructure.Persistence.Repositories;
using Mundialito.Infrastructure.Queries.Common;
using Mundialito.Infrastructure.Seeding;

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

        // Lado Command (§3): EF Core + UnitOfWork.
        services.AddDbContext<MundialitoDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<ITeamStandingRepository, TeamStandingRepository>();

        // Lado Query (§3): exclusivamente Dapper. Los handlers de lectura viven aquí, junto a su SQL.
        services.AddSingleton(new DbConnectionFactory(connectionString));
        services.AddHandlersFromAssembly(typeof(DependencyInjection).Assembly);

        // Idempotencia de los POST (§6).
        services.AddScoped<IIdempotencyStore, SqlIdempotencyStore>();

        // Al arrancar: migraciones (con reintentos) y seed si la base está vacía (§9).
        services.AddScoped<DataSeeder>();
        services.AddHostedService<DatabaseInitializer>();

        return services;
    }
}
