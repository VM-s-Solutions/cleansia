using Cleansia.Core.Domain.EmployeePayroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

/// <summary>
/// EF config for the payout-reference counter (ADR-0046). Deliberately a plain
/// <see cref="IEntityTypeConfiguration{TEntity}"/> rather than
/// <see cref="AuditableEntityConfiguration{T,TKey}"/>: the entity is tenant-global by design, so there
/// is no <c>TenantId</c> column and the S8 tenant grep should treat this as a reasoned exception —
/// the same lane <see cref="ProcessedMessageEntityConfiguration"/> takes.
/// </summary>
public class PayoutReferenceCounterEntityConfiguration : IEntityTypeConfiguration<PayoutReferenceCounter>
{
    public void Configure(EntityTypeBuilder<PayoutReferenceCounter> builder)
    {
        builder.ToTable("PayoutReferenceCounters");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.Year).IsRequired();

        builder.Property(e => e.Value).IsRequired();

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.CreatedOn).IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(255);

        builder.Property(e => e.UpdatedOn)
            .IsRequired(false);

        // The allocator's atomic UPSERT keys on this index, and (Year) is the WHOLE key — no scope, no
        // tenant. A second column in the arbiter is what ADR-0046 §D2.1 forbids: it would be a
        // discriminator over a one-member namespace, and a nullable one would re-arm the
        // nulls-distinct collapse FiscalCounters answers with AreNullsDistinct(false).
        builder.HasIndex(e => e.Year)
            .IsUnique();
    }
}
