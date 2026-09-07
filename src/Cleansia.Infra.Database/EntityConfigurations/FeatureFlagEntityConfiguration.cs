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

        // ScopeValue is null for every GLOBAL flag, which is every flag that exists — so with nulls
        // distinct this index enforced nothing on the only scope in use. Two rows for one name
        // cannot both be honoured: IsFeatureEnabledAsync takes FirstOrDefault with no ORDER BY, so
        // the flag would flip between requests. Unfiltered, because here the null is a key VALUE
        // ("global, no qualifier"), not an absence.
        builder.HasIndex(e => new { e.Name, e.Scope, e.ScopeValue })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
