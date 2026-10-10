using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mundialito.Infrastructure.Idempotency;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");

        // La key es la clave primaria: la base garantiza que no haya dos filas con la misma.
        builder.HasKey(r => r.IdempotencyKey);
        builder.Property(r => r.IdempotencyKey).HasMaxLength(IdempotencyRecord.KeyMaxLength);

        builder.Property(r => r.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();
        builder.Property(r => r.ResponseLocation).HasMaxLength(500);
    }
}
