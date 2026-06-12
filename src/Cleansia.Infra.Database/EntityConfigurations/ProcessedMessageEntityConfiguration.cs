using Cleansia.Core.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

/// <summary>
/// EF config for the durable idempotency claim row. Mirrors
/// <see cref="ProcessedStripeEventEntityConfiguration"/>. Deliberately a plain
/// <see cref="IEntityTypeConfiguration{TEntity}"/> (NOT an auditable/tenant base): the entity is
/// tenant-global by design (see <see cref="ProcessedMessage"/>) — there is no <c>TenantId</c> column, so
/// the S8 tenant grep should treat this as a reasoned exception.
/// </summary>
public class ProcessedMessageEntityConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("ProcessedMessages");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.MessageKey)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ProcessedAt)
            .IsRequired();

        // The load-bearing constraint. The guard's claim relies on a Postgres 23505 here to collapse the
        // parallel-retry / scale-out race: two consumers can both miss the existence check and both
        // attempt the insert — only one commits, the other's DbUpdateException is caught → already-claimed.
        builder.HasIndex(e => e.MessageKey)
            .IsUnique();
    }
}
