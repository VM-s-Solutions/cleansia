using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CustomerActionAuditConfiguration : BaseEntityConfiguration<CustomerActionAudit, string>
{
    public override void Configure(EntityTypeBuilder<CustomerActionAudit> builder)
    {
        base.Configure(builder);

        builder.ToTable("CustomerActionAudits");

        // BaseEntityConfiguration maps only the key; the tenant column, its foreign key and its filter
        // live on TenantAuditableEntityConfiguration, which this entity does not inherit. The global
        // query filter is applied generically in CleansiaDbContext.ApplyTenantQueryFilters for any
        // ITenantEntity; the column and the FK must still be mapped here, NOT NULL and Restrict like
        // every other stamped table.
        builder.Property(e => e.TenantId)
            .HasMaxLength(26)
            .IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.UserId)
            .HasMaxLength(26);

        builder.Property(e => e.ClientAudience)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(e => e.IpAddress)
            .HasMaxLength(45);

        builder.Property(e => e.DeviceLabel)
            .HasMaxLength(120);

        builder.Property(e => e.DeviceId)
            .HasMaxLength(64);

        builder.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.ResourceType)
            .HasMaxLength(50);

        builder.Property(e => e.ResourceId)
            .HasMaxLength(CustomerActionAudit.ResourceIdMaxLength);

        builder.Property(e => e.Success)
            .IsRequired();

        builder.Property(e => e.ErrorCode)
            .HasMaxLength(CustomerActionAudit.ErrorCodeMaxLength);

        builder.Property(e => e.OccurredOn)
            .IsRequired();

        builder.Property(e => e.PayloadJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(64);

        builder.HasIndex(e => new { e.TenantId, e.OccurredOn })
            .IsDescending(false, true)
            .HasDatabaseName("IX_CustomerActionAudits_TenantId_OccurredOn");

        builder.HasIndex(e => new { e.UserId, e.OccurredOn })
            .IsDescending(false, true)
            .HasDatabaseName("IX_CustomerActionAudits_UserId_OccurredOn");

        builder.HasIndex(e => new { e.ResourceType, e.ResourceId })
            .HasDatabaseName("IX_CustomerActionAudits_ResourceType_ResourceId");

        // The retention sweep deletes by row age across tenants (ADR-0062 D5); no (Action, ...) index —
        // the action filter runs inside the tenant-scoped list.
        builder.HasIndex(e => e.OccurredOn)
            .HasDatabaseName("IX_CustomerActionAudits_OccurredOn");
    }
}
