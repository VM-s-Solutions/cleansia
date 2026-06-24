using Cleansia.Core.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class AdminActionAuditConfiguration : BaseEntityConfiguration<AdminActionAudit, string>
{
    public override void Configure(EntityTypeBuilder<AdminActionAudit> builder)
    {
        base.Configure(builder);

        builder.ToTable("AdminActionAudits");

        // BaseEntityConfiguration maps only the key; TenantId + its filter live on
        // AuditableEntityConfiguration, which this entity does not inherit. The global query
        // filter is applied generically in CleansiaDbContext.ApplyTenantQueryFilters for any
        // ITenantEntity; the property itself must still be mapped here.
        builder.Property(e => e.TenantId)
            .HasMaxLength(26)
            .IsRequired(false);

        builder.Property(e => e.ActorId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.ActorEmail)
            .HasMaxLength(255);

        builder.Property(e => e.ActorProfile)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ResourceType)
            .HasMaxLength(100);

        builder.Property(e => e.ResourceId)
            .HasMaxLength(64);

        builder.Property(e => e.Success)
            .IsRequired();

        builder.Property(e => e.ErrorCode)
            .HasMaxLength(200);

        builder.Property(e => e.OccurredOn)
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasMaxLength(1000);

        builder.Property(e => e.BeforeJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.AfterJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(64);

        builder.HasIndex(e => new { e.TenantId, e.OccurredOn })
            .IsDescending(false, true)
            .HasDatabaseName("IX_AdminActionAudits_TenantId_OccurredOn");

        builder.HasIndex(e => new { e.ResourceType, e.ResourceId })
            .HasDatabaseName("IX_AdminActionAudits_ResourceType_ResourceId");

        builder.HasIndex(e => new { e.ActorId, e.OccurredOn })
            .IsDescending(false, true)
            .HasDatabaseName("IX_AdminActionAudits_ActorId_OccurredOn");

        builder.HasIndex(e => new { e.Action, e.OccurredOn })
            .IsDescending(false, true)
            .HasDatabaseName("IX_AdminActionAudits_Action_OccurredOn");
    }
}
