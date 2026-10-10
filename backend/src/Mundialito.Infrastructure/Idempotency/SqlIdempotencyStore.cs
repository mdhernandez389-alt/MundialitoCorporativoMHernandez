using Dapper;
using Mundialito.Application.Abstractions.Idempotency;
using Mundialito.Infrastructure.Queries.Common;

namespace Mundialito.Infrastructure.Idempotency;

/// <summary>
/// Implementación con SQL directo (Dapper), no con EF Core ni el UnitOfWork, a propósito:
/// <list type="bullet">
/// <item>No es un dato del dominio ni un Command: es un registro HTTP que debe confirmarse ANTES de ejecutar
/// el Command (la reserva) y por separado de su transacción.</item>
/// <item>La reserva tiene que ser atómica, y eso es un solo INSERT ... WHERE NOT EXISTS con bloqueo,
/// algo que EF Core no puede expresar.</item>
/// </list>
/// Nunca toca el DbContext, así que la regla del §4 (no guardar desde el DbContext fuera del UoW) se mantiene.
/// </summary>
internal sealed class SqlIdempotencyStore(DbConnectionFactory connections) : IIdempotencyStore
{
    public async Task<IdempotencyReservation> TryReserveAsync(
        string key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        // UPDLOCK + HOLDLOCK: si dos requests ejecutan esto a la vez con la misma key, el segundo espera a que
        // termine el INSERT del primero y luego ve la fila: solo uno inserta. Sin excepciones de por medio.
        const string sql = $"""
            INSERT INTO IdempotencyRecords (IdempotencyKey, RequestHash, Status, CreatedAtUtc)
            SELECT @Key, @RequestHash, '{IdempotencyRecord.InProgress}', SYSUTCDATETIME()
            WHERE NOT EXISTS (
                SELECT 1 FROM IdempotencyRecords WITH (UPDLOCK, HOLDLOCK) WHERE IdempotencyKey = @Key);

            DECLARE @Reserved bit = CASE WHEN @@ROWCOUNT = 1 THEN 1 ELSE 0 END;

            SELECT @Reserved AS Reserved, RequestHash, Status, ResponseStatusCode, ResponseBody, ResponseLocation
            FROM IdempotencyRecords
            WHERE IdempotencyKey = @Key;
            """;

        await using var connection = connections.Create();
        var row = await connection.QuerySingleAsync<ReservationRow>(
            new CommandDefinition(sql, new { Key = key, RequestHash = requestHash }, cancellationToken: cancellationToken));

        if (row.Reserved)
        {
            return new IdempotencyReservation(IdempotencyOutcome.Reserved);
        }

        // Misma key con otro contenido: no se compara el estado, es un error del cliente en cualquier caso.
        if (row.RequestHash != requestHash)
        {
            return new IdempotencyReservation(IdempotencyOutcome.KeyReused);
        }

        if (row.Status == IdempotencyRecord.Completed)
        {
            return new IdempotencyReservation(
                IdempotencyOutcome.Completed,
                new IdempotentResponse(row.ResponseStatusCode!.Value, row.ResponseBody!, row.ResponseLocation));
        }

        return new IdempotencyReservation(IdempotencyOutcome.InProgress);
    }

    public async Task CompleteAsync(string key, IdempotentResponse response, CancellationToken cancellationToken)
    {
        const string sql = $"""
            UPDATE IdempotencyRecords
            SET Status = '{IdempotencyRecord.Completed}',
                ResponseStatusCode = @StatusCode,
                ResponseBody = @Body,
                ResponseLocation = @Location,
                CompletedAtUtc = SYSUTCDATETIME()
            WHERE IdempotencyKey = @Key;
            """;

        await using var connection = connections.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Key = key, response.StatusCode, response.Body, response.Location },
            cancellationToken: cancellationToken));
    }

    public async Task ReleaseAsync(string key, CancellationToken cancellationToken)
    {
        // Solo borra reservas en curso: una respuesta ya completada nunca se pierde.
        const string sql = $"""
            DELETE FROM IdempotencyRecords
            WHERE IdempotencyKey = @Key AND Status = '{IdempotencyRecord.InProgress}';
            """;

        await using var connection = connections.Create();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Key = key }, cancellationToken: cancellationToken));
    }

    private sealed class ReservationRow
    {
        public bool Reserved { get; init; }

        public string RequestHash { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public int? ResponseStatusCode { get; init; }

        public string? ResponseBody { get; init; }

        public string? ResponseLocation { get; init; }
    }
}
