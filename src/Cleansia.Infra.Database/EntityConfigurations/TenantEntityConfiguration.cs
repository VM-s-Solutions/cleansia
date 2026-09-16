using Cleansia.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class TenantEntityConfiguration : AuditableEntityConfiguration<Tenant, string>
{
    public override void Configure(EntityTypeBuilder<Tenant> builder)
    {
        base.Configure(builder);

        builder.ToTable("Tenants");

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.WindDownRequestedBy)
            .HasMaxLength(26);

        builder.Property(t => t.ArchiveRequestedBy)
            .HasMaxLength(26);

        builder.Property(t => t.ArchiveManifestSha256)
            .HasColumnType("character(64)");
    }
}
