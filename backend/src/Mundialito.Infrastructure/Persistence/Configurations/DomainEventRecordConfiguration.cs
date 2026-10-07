using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mundialito.Infrastructure.Persistence.Configurations;

internal sealed class DomainEventRecordConfiguration : IEntityTypeConfiguration<DomainEventRecord>
{
    public void Configure(EntityTypeBuilder<DomainEventRecord> builder)
    {
        builder.ToTable("DomainEvents");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Type).HasMaxLength(DomainEventRecord.TypeMaxLength).IsRequired();
        builder.Property(e => e.Payload).IsRequired();

        builder.HasIndex(e => e.OccurredOnUtc);
    }
}
