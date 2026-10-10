using Microsoft.Extensions.DependencyInjection;
using Mundialito.Application.Abstractions.Idempotency;

namespace Mundialito.IntegrationTests.Idempotency;

/// <summary>Persistencia de la Idempotency-Key y reserva atómica (§6, §15) contra SQL Server real.</summary>
[Collection(nameof(SqlServerCollection))]
public sealed class SqlIdempotencyStoreTests(SqlServerFixture fixture)
{
    private const string Hash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string OtherHash = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    [Fact]
    public async Task Ciclo_de_vida_de_una_key()
    {
        var key = Guid.NewGuid().ToString();
        var response = new IdempotentResponse(201, """{"id":"42"}""", "/api/teams/42");

        Assert.Equal(IdempotencyOutcome.Reserved, (await ReserveAsync(key, Hash)).Outcome);
        Assert.Equal(IdempotencyOutcome.InProgress, (await ReserveAsync(key, Hash)).Outcome);
        Assert.Equal(IdempotencyOutcome.KeyReused, (await ReserveAsync(key, OtherHash)).Outcome);

        await UseStoreAsync(store => store.CompleteAsync(key, response, CancellationToken.None));

        var repeated = await ReserveAsync(key, Hash);
        Assert.Equal(IdempotencyOutcome.Completed, repeated.Outcome);
        Assert.Equal(response, repeated.Response);
    }

    [Fact]
    public async Task Liberar_una_reserva_permite_volver_a_usar_la_key()
    {
        var key = Guid.NewGuid().ToString();
        await ReserveAsync(key, Hash);

        await UseStoreAsync(store => store.ReleaseAsync(key, CancellationToken.None));

        Assert.Equal(IdempotencyOutcome.Reserved, (await ReserveAsync(key, OtherHash)).Outcome);
    }

    [Fact]
    public async Task Una_respuesta_completada_no_se_puede_liberar()
    {
        var key = Guid.NewGuid().ToString();
        await ReserveAsync(key, Hash);
        await UseStoreAsync(store => store.CompleteAsync(key, new IdempotentResponse(201, "{}", null), CancellationToken.None));

        await UseStoreAsync(store => store.ReleaseAsync(key, CancellationToken.None));

        Assert.Equal(IdempotencyOutcome.Completed, (await ReserveAsync(key, Hash)).Outcome);
    }

    [Fact]
    public async Task De_diez_reservas_simultaneas_con_la_misma_key_solo_una_gana()
    {
        var key = Guid.NewGuid().ToString();

        // Cada intento con su propio scope y conexión, como diez requests distintos.
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => ReserveAsync(key, Hash))));

        Assert.Single(outcomes, o => o.Outcome == IdempotencyOutcome.Reserved);
        Assert.All(outcomes.Where(o => o.Outcome != IdempotencyOutcome.Reserved),
            o => Assert.Equal(IdempotencyOutcome.InProgress, o.Outcome));
    }

    private async Task<IdempotencyReservation> ReserveAsync(string key, string hash)
    {
        IdempotencyReservation reservation = null!;
        await UseStoreAsync(async store => reservation = await store.TryReserveAsync(key, hash, CancellationToken.None));
        return reservation;
    }

    private async Task UseStoreAsync(Func<IIdempotencyStore, Task> action)
    {
        await using var scope = fixture.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<IIdempotencyStore>());
    }
}
