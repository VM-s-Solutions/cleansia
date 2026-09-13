using Cleansia.Core.Domain.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class BaseEntityConfiguration<T, TKey> : IEntityTypeConfiguration<T>
    where T : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasMaxLength(26)
            .IsRequired();
    }
}

public class AuditableEntityConfiguration<T, TKey> : BaseEntityConfiguration<T, TKey>
    where T : Auditable
{
    // The two infra tables that carry a tenant only when the envelope has one: an outbox row enqueued
    // from a context with no tenant, and a poison body whose tenant could not be read.
    public static readonly IReadOnlySet<Type> TenantIdNullableTypes = new HashSet<Type>
    {
        typeof(Cleansia.Core.Domain.Outbox.OutboxMessage),
        typeof(Cleansia.Core.Domain.DeadLettering.DeadLetter),
    };

    public override void Configure(EntityTypeBuilder<T> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.TenantId)
            .HasMaxLength(26)
            .IsRequired(false);

        // NOT NULL on every stamped table (ADR-0061 D8): a writer that forgets its market fails loudly
        // on first use with a 23502 instead of landing rows no tenanted reader can see. The CLR type
        // stays string? because the value is set at commit time. Auditable tables that are not
        // ITenantEntity keep the dead nullable column.
        if (typeof(ITenantEntity).IsAssignableFrom(typeof(T)) && !TenantIdNullableTypes.Contains(typeof(T)))
        {
            builder.Property(e => e.TenantId).IsRequired();
        }

        builder.HasIndex(e => e.TenantId);

        builder.Property(e => e.CreatedBy)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.CreatedOn)
            .IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(255);

        builder.Property(e => e.UpdatedOn)
            .IsRequired(false);

        builder.Property(e => e.DeactivatedBy)
            .HasMaxLength(255);

        builder.Property(e => e.DeactivatedOn)
            .IsRequired(false);
    }
}