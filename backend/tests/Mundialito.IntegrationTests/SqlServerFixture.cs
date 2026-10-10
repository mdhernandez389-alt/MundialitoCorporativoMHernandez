using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Observability;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Standings;
using Mundialito.Application.Teams;
using Mundialito.Domain.Standings;
using Mundialito.Domain.Teams;
using Mundialito.Infrastructure;
using Mundialito.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace Mundialito.IntegrationTests;

/// <summary>
/// SQL Server real y desechable (Testcontainers): se crea al empezar la suite, se migra y se destruye al final.
/// Se usa SQL Server y no InMemory/SQLite porque lo que se prueba es propio de SQL Server: transacciones,
/// rowversion, índices únicos con sus códigos de error y los bloqueos UPDLOCK/HOLDLOCK.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = _container.GetConnectionString(),
            })
            .Build();

        // La misma Infrastructure que usa la API (DbContext, UnitOfWork, repositorios, Dapper, idempotencia).
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<TestCorrelationContext>();
        services.AddScoped<ICorrelationContext>(provider => provider.GetRequiredService<TestCorrelationContext>());
        services.AddInfrastructure(configuration);
        _services = services.BuildServiceProvider(validateScopes: true);

        await using var scope = CreateScope();
        await scope.ServiceProvider.GetRequiredService<MundialitoDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>Un scope = un "request": su propio DbContext, UnitOfWork y repositorios.</summary>
    public AsyncServiceScope CreateScope() => _services.CreateAsyncScope();

    /// <summary>Crea y confirma un equipo con su fila de tabla, como lo hace CreateTeam. Nombre único por test.</summary>
    public async Task<Team> CreateTeamAsync(string name)
    {
        await using var scope = CreateScope();
        var team = Team.Create(name, "Pruebas").Value;
        scope.ServiceProvider.GetRequiredService<ITeamRepository>().Add(team);
        scope.ServiceProvider.GetRequiredService<ITeamStandingRepository>().Add(TeamStanding.Create(team.Id));

        var commit = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(CancellationToken.None);
        Assert.True(commit.IsSuccess);

        return team;
    }

    /// <summary>Nombre único: los tests comparten la base y no deben chocar entre sí.</summary>
    public static string UniqueName(string name) => $"{name} {Guid.NewGuid().ToString("N")[..8]}";
}

/// <summary>Hace las veces del CorrelationContext que en la API llena el middleware.</summary>
public sealed class TestCorrelationContext : ICorrelationContext
{
    public string? CorrelationId { get; set; }
}

[CollectionDefinition(nameof(SqlServerCollection))]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
