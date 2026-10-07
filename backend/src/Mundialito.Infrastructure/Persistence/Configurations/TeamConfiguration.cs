using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mundialito.Domain.Teams;

namespace Mundialito.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    /// <summary>Índice del nombre único. El UnitOfWork lo reconoce para traducir el duplicado a un error de negocio.</summary>
    public const string NameIndex = "UX_Teams_Name";

    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");

        builder.HasKey(t => t.Id);
        // El Id lo genera el dominio. Sin esto EF cree que una entidad nueva con Id ya existe y hace UPDATE.
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(Team.NameMaxLength).IsRequired();
        builder.Property(t => t.Department).HasMaxLength(Team.DepartmentMaxLength).IsRequired();

        // Cubre la carrera entre dos requests que crean el mismo nombre a la vez (el handler revisa antes).
        builder.HasIndex(t => t.Name).IsUnique().HasDatabaseName(NameIndex);

        // Concurrencia optimista: dos PUT simultáneos al mismo equipo → el segundo recibe 409.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.Ignore(t => t.DomainEvents);
    }
}
