using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mundialito.Infrastructure.Persistence;

namespace Mundialito.Infrastructure.Seeding;

/// <summary>
/// Prepara la base al arrancar (§9): espera a que SQL Server responda, aplica las migraciones pendientes
/// y siembra los datos de ejemplo si está vacía. Así <c>docker-compose up</c> funciona desde cero.
/// </summary>
/// <remarks>
/// Es un <see cref="IHostedService"/>: corre antes de que la API empiece a aceptar requests, así nadie
/// consulta tablas que todavía no existen.
/// </remarks>
internal sealed class DatabaseInitializer(IServiceScopeFactory scopeFactory, ILogger<DatabaseInitializer> logger)
    : IHostedService
{
    private const int MaxAttempts = 10;
    private const int MaxDelaySeconds = 10;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // DbContext, repositorios y UnitOfWork son Scoped: fuera de un request hay que crear el scope a mano.
        await using var scope = scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<MundialitoDbContext>();
        await MigrateWithRetriesAsync(dbContext, cancellationToken);

        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedIfEmptyAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// En Docker, SQL Server tarda 10-20 s en estar listo y la API arranca en 2: sin reintentos, el primer
    /// intento falla y el contenedor de la API muere. Espera 2, 4, 6... hasta 10 s entre intentos.
    /// </summary>
    private async Task MigrateWithRetriesAsync(MundialitoDbContext dbContext, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pending.Count > 0)
                {
                    logger.LogInformation("Aplicando {MigrationCount} migraciones pendientes", pending.Count);
                    // EF Core toma un lock en la base: dos instancias de la API no migran a la vez.
                    await dbContext.Database.MigrateAsync(cancellationToken);
                }

                return;
            }
            // Que la base no responda al arrancar es una condición externa esperada, no un bug: se reintenta.
            // Agotados los intentos, la excepción sigue y la API no arranca (mejor que arrancar sin base).
            catch (Exception exception) when (attempt < MaxAttempts && FindSqlException(exception) is not null)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(2 * attempt, MaxDelaySeconds));
                logger.LogWarning(
                    "Base de datos no disponible (error SQL {SqlErrorNumber}); reintento {Attempt}/{MaxAttempts} en {DelaySeconds} s",
                    FindSqlException(exception)!.Number,
                    attempt,
                    MaxAttempts,
                    delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// El fallo de conexión llega de dos formas: un <see cref="SqlException"/> directo (servidor apagado) o
    /// envuelto por EF Core en un <see cref="InvalidOperationException"/> "transient failure" (servidor arrancando:
    /// acepta la conexión pero la corta en el handshake). Ambos significan "todavía no está lista".
    /// </summary>
    private static SqlException? FindSqlException(Exception exception) =>
        exception as SqlException ?? exception.InnerException as SqlException;
}
