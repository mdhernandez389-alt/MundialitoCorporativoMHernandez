using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Players;
using Mundialito.Domain.Teams;

namespace Mundialito.Infrastructure.Persistence.Configurations;

internal sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("Goals");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        // La FK al partido se configura en MatchConfiguration (dueño de la relación).
        builder.HasOne<Player>().WithMany().HasForeignKey(g => g.PlayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>().WithMany().HasForeignKey(g => g.TeamId).OnDelete(DeleteBehavior.Restrict);

        // Propiedad de solo lectura: EF no la mapea por convención. Los goles ya guardados quedan en false.
        builder.Property(g => g.IsOwnGoal);

        builder.Ignore(g => g.DomainEvents);
    }
}
