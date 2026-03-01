using Cleansia.Core.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class FeatureFlagEntityConfiguration : AuditableEntityConfiguration<FeatureFlag, string>
{
    public override void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        builder.Property(e => e.Scope)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.ScopeValue)
            .HasMaxLength(26);

        builder.HasIndex(e => new { e.Name, e.Scope, e.ScopeValue })
            .IsUnique();
    }
}
