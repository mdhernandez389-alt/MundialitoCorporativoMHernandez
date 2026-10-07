using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mundialito.Domain.Players;
using Mundialito.Domain.Teams;

namespace Mundialito.Infrastructure.Persistence.Configurations;

internal sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    /// <summary>Un dorsal por equipo. El UnitOfWork lo reconoce para traducir el duplicado a un error de negocio.</summary>
    public const string ShirtNumberIndex = "UX_Players_TeamId_ShirtNumber";

    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("Players");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.FullName).HasMaxLength(Player.FullNameMaxLength).IsRequired();

        // Relación solo por Id (agregados distintos): FK en la base, sin propiedad de navegación en el dominio.
        // Cascade: un equipo solo se borra si no tiene partidos (lo valida el handler), y sin partidos
        // no hay goles, así que sus jugadores se pueden ir con él sin dejar huecos en goleadores.
        builder.HasOne<Team>().WithMany().HasForeignKey(p => p.TeamId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.TeamId, p.ShirtNumber }).IsUnique().HasDatabaseName(ShirtNumberIndex);

        builder.Ignore(p => p.DomainEvents);
    }
}
