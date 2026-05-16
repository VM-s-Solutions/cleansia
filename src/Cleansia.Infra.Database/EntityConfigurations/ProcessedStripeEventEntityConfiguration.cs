using Cleansia.Core.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class ProcessedStripeEventEntityConfiguration : IEntityTypeConfiguration<ProcessedStripeEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedStripeEvent> builder)
    {
        builder.ToTable("ProcessedStripeEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.StripeEventId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.StripeCreatedAt)
            .IsRequired();

        builder.Property(e => e.ProcessedAt)
            .IsRequired();

        // Unique index is the load-bearing constraint for replay safety.
        // The handler relies on `DbUpdateException` (PG 23505) here to catch
        // parallel-retry races — both retries pass the existence check, only
        // one INSERT commits.
        builder.HasIndex(e => e.StripeEventId)
            .IsUnique();
    }
}
