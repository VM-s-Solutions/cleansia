using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Tenancy;
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
    public override void Configure(EntityTypeBuilder<T> builder)
    {
        base.Configure(builder);

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

public class TenantAuditableEntityConfiguration<T, TKey> : AuditableEntityConfiguration<T, TKey>
    where T : TenantAuditable
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

        // NOT NULL on every stamped table (ADR-0061 D8): a writer that forgets its market fails loudly
        // on first use with a 23502 instead of landing rows no tenanted reader can see. The CLR type
        // stays string? because the value is set at commit time.
        builder.Property(e => e.TenantId)
            .HasMaxLength(26)
            .IsRequired(!TenantIdNullableTypes.Contains(typeof(T)));

        // A real foreign key, so a tenant id that is not an operating company fails 23503 on first use
        // instead of landing rows no reader of any company can see. Restrict: a company is never deleted
        // out from under its rows. No navigation — nothing in production reads Tenants.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.TenantId);
    }
}
