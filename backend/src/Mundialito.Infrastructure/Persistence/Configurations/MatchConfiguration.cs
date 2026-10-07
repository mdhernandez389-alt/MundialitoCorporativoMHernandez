using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Teams;

namespace Mundialito.Infrastructure.Persistence.Configurations;

internal sealed class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.ToTable("Matches");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.HasOne<Team>().WithMany().HasForeignKey(m => m.HomeTeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>().WithMany().HasForeignKey(m => m.AwayTeamId).OnDelete(DeleteBehavior.Restrict);

        // Texto ("Scheduled"/"Played") en vez de número: la base se entiende sola y Dapper filtra legible.
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

        // Para los filtros por fecha del paso 9.
        builder.HasIndex(m => m.ScheduledAtUtc);

        // Value object en las mismas filas (columnas HomeGoals / AwayGoals), null mientras no se juega.
        builder.ComplexProperty(m => m.Score, score =>
        {
            score.Property(s => s.HomeGoals).HasColumnName("HomeGoals");
            score.Property(s => s.AwayGoals).HasColumnName("AwayGoals");
        });

        // Match es dueño de sus goles: EF llena la lista privada _goals y borra los goles con el partido.
        builder.HasMany(m => m.Goals).WithOne().HasForeignKey(g => g.MatchId).OnDelete(DeleteBehavior.Cascade);

        // Concurrencia optimista: dos resultados simultáneos del mismo partido → el segundo recibe 409
        // en vez de sumar el partido dos veces a la tabla.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.Ignore(m => m.DomainEvents);
    }
}
