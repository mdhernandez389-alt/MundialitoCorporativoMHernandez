using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mundialito.Application.Abstractions.Observability;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Abstractions;
using Mundialito.Domain.Players;
using Mundialito.Domain.Results;
using Mundialito.Domain.Teams;
using Mundialito.Infrastructure.Persistence.Configurations;

namespace Mundialito.Infrastructure.Persistence;

/// <summary>
/// Implementación del §4: abre la transacción, registra los eventos de dominio, guarda y confirma.
/// Comparte el <see cref="MundialitoDbContext"/> (scoped) con los repositorios, así confirma todo lo que ellos rastrearon.
/// </summary>
internal sealed class UnitOfWork(
    MundialitoDbContext dbContext,
    ICorrelationContext correlation,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    // Números de error de SQL Server que significan "chocaste con otro request", no "la base falló".
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;
    private const int ForeignKeyViolation = 547;

    /// <summary>
    /// Qué error de negocio corresponde a cada índice único. Un índice que no esté aquí
    /// cae en <see cref="PersistenceErrors.DuplicateKey"/>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Error> UniqueIndexErrors = new Dictionary<string, Error>
    {
        [TeamConfiguration.NameIndex] = TeamErrors.NameDuplicated,
        [PlayerConfiguration.ShirtNumberIndex] = PlayerErrors.ShirtNumberTaken,
    };

    public async Task<Result> CommitAsync(CancellationToken cancellationToken)
    {
        var domainEvents = RecordDomainEvents();

        logger.LogDebug(
            "Confirmando {ChangeCount} cambios y {EventCount} eventos de dominio",
            dbContext.ChangeTracker.Entries().Count(entry => entry.State is not EntityState.Unchanged),
            domainEvents.Count);

        // Si no se llega al commit (error o excepción), el "await using" hace rollback al salir.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await dbContext.SaveChangesFromUnitOfWorkAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (TryTranslate(exception, out var error))
        {
            logger.LogWarning("Commit rechazado por la base: {ErrorCode}", error.Code);
            return error;
        }

        // Solo después del commit: un evento loggeado es un evento que de verdad ocurrió.
        // Cada log lleva el TraceId y el CorrelationId del request (los agrega el pipeline de logging).
        foreach (var domainEvent in domainEvents)
        {
            var eventType = domainEvent.GetType().Name;

            logger.LogInformation(
                "Evento de dominio {EventType} registrado ({EventId}): {DomainEvent}",
                eventType,
                domainEvent.EventId,
                domainEvent);

            // También queda marcado dentro de la traza: en Jaeger se ve en qué punto del request ocurrió.
            Activity.Current?.AddEvent(new ActivityEvent(
                eventType,
                tags: new ActivityTagsCollection { ["domain_event.id"] = domainEvent.EventId.ToString() }));
        }

        return Result.Success();
    }

    /// <summary>
    /// Saca los eventos pendientes de las entidades rastreadas y los agrega como filas de <c>DomainEvents</c>,
    /// para que se guarden en la misma transacción que los cambios.
    /// </summary>
    private List<IDomainEvent> RecordDomainEvents()
    {
        var entitiesWithEvents = dbContext.ChangeTracker.Entries<Entity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = entitiesWithEvents.SelectMany(entity => entity.DomainEvents).ToList();

        // Se limpian para que un segundo commit en el mismo request no los registre dos veces.
        entitiesWithEvents.ForEach(entity => entity.ClearDomainEvents());

        // Cada evento queda sellado con el request que lo generó (§7: "mantener trazabilidad").
        var traceId = Activity.Current?.TraceId.ToHexString();
        dbContext.DomainEvents.AddRange(
            domainEvents.Select(domainEvent => DomainEventRecord.From(domainEvent, traceId, correlation.CorrelationId)));

        return domainEvents;
    }

    /// <summary>
    /// Traduce los rechazos esperados de la base a un <see cref="Error"/> de conflicto.
    /// Cualquier otro fallo (base caída, timeout) no se traduce: sigue como excepción → 500.
    /// </summary>
    private static bool TryTranslate(DbUpdateException exception, out Error error)
    {
        error = exception switch
        {
            DbUpdateConcurrencyException => PersistenceErrors.ConcurrencyConflict,
            { InnerException: SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sql } =>
                UniqueViolationError(sql.Message),
            { InnerException: SqlException { Number: ForeignKeyViolation } } => PersistenceErrors.ReferenceConflict,
            _ => Error.None
        };

        return error != Error.None;
    }

    // SQL Server incluye el nombre del índice en el mensaje: "...with unique index 'UX_Teams_Name'...".
    private static Error UniqueViolationError(string sqlMessage)
    {
        foreach (var (indexName, error) in UniqueIndexErrors)
        {
            if (sqlMessage.Contains(indexName, StringComparison.Ordinal))
            {
                return error;
            }
        }

        return PersistenceErrors.DuplicateKey;
    }
}
