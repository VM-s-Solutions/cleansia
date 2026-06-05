using Cleansia.Core.Domain.DeadLettering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

/// <summary>
/// ADR-0002 D3 (F3) — EF config for the durable dead-letter row written by the <c>-poison</c>
/// consumers. Inherits the <see cref="AuditableEntityConfiguration{T,TKey}"/> base so it picks up the
/// Id/TenantId/audit columns + the TenantId index (TenantId is nullable — a poison body may be
/// unparseable so the tenant can be unknown).
/// </summary>
public class DeadLetterEntityConfiguration : AuditableEntityConfiguration<DeadLetter, string>
{
    public override void Configure(EntityTypeBuilder<DeadLetter> builder)
    {
        base.Configure(builder);

        builder.ToTable("DeadLetters");

        builder.Property(e => e.SourceQueue)
            .IsRequired()
            .HasMaxLength(128);

        // Raw, verbatim poisoned body — unbounded text so nothing is truncated (the fiscal
        // recovery/replay source).
        builder.Property(e => e.RawBody)
            .IsRequired()
            .HasColumnType("text");

        // Optional error/exception text — unbounded so a full stack trace fits.
        builder.Property(e => e.Error)
            .IsRequired(false)
            .HasColumnType("text");

        builder.Property(e => e.DeadLetteredAt)
            .IsRequired();

        // Admin views list by queue + recency.
        builder.HasIndex(e => e.SourceQueue);
        builder.HasIndex(e => e.DeadLetteredAt);
    }
}
