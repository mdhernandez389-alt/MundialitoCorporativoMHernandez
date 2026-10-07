using Microsoft.EntityFrameworkCore;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Players;
using Mundialito.Domain.Standings;
using Mundialito.Domain.Teams;

namespace Mundialito.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core del lado de escritura. Es <c>internal</c>: fuera de Infrastructure nadie lo ve.
/// </summary>
/// <remarks>
/// Guardia del §4: <c>SaveChanges</c> está bloqueado. Solo <see cref="UnitOfWork"/> puede guardar,
/// a través de <see cref="SaveChangesFromUnitOfWorkAsync"/>.
/// </remarks>
internal sealed class MundialitoDbContext(DbContextOptions<MundialitoDbContext> options) : DbContext(options)
{
    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<Match> Matches => Set<Match>();

    public DbSet<TeamStanding> TeamStandings => Set<TeamStanding>();

    public DbSet<DomainEventRecord> DomainEvents => Set<DomainEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MundialitoDbContext).Assembly);

    // Las demás sobrecargas (SaveChanges(), SaveChangesAsync(ct)) terminan en estas dos.
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw SavingOutsideUnitOfWork();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw SavingOutsideUnitOfWork();

    /// <summary>Única puerta real de guardado. La usa solo <see cref="UnitOfWork"/>.</summary>
    internal Task<int> SaveChangesFromUnitOfWorkAsync(CancellationToken cancellationToken) =>
        base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    // Llamar SaveChanges directo es un bug del programador, no un caso de negocio: falla ruidosamente.
    private static InvalidOperationException SavingOutsideUnitOfWork() =>
        new("No se permite guardar desde el DbContext: usa IUnitOfWork.CommitAsync.");
}
