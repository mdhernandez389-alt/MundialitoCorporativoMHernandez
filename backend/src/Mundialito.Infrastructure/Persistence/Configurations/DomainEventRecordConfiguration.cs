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

        // TraceId W3C: 32 caracteres hexadecimales. Con él se cruza el evento con sus logs y su traza en Jaeger.
        builder.Property(e => e.TraceId).HasMaxLength(32).IsFixedLength();
        builder.Property(e => e.CorrelationId).HasMaxLength(100);

        builder.HasIndex(e => e.OccurredOnUtc);
        builder.HasIndex(e => e.TraceId);
    }
}
