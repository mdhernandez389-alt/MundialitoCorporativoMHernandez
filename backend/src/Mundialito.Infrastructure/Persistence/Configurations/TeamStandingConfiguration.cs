using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mundialito.Domain.Standings;
using Mundialito.Domain.Teams;

namespace Mundialito.Infrastructure.Persistence.Configurations;

internal sealed class TeamStandingConfiguration : IEntityTypeConfiguration<TeamStanding>
{
    public void Configure(EntityTypeBuilder<TeamStanding> builder)
    {
        builder.ToTable("TeamStandings");

        // Una fila por equipo: el propio TeamId es la clave.
        builder.HasKey(s => s.TeamId);
        builder.Property(s => s.TeamId).ValueGeneratedNever();

        // La fila de la tabla es parte del equipo: si el equipo se borra, su fila también.
        builder.HasOne<Team>().WithOne().HasForeignKey<TeamStanding>(s => s.TeamId).OnDelete(DeleteBehavior.Cascade);

        // Valores derivados: no se guardan (se calculan de Won/Drawn/GoalsFor/GoalsAgainst).
        builder.Ignore(s => s.Points);
        builder.Ignore(s => s.GoalDifference);

        // Concurrencia optimista: dos partidos del mismo equipo registrados a la vez no pierden una suma.
        builder.Property<byte[]>("RowVersion").IsRowVersion();
    }
}
