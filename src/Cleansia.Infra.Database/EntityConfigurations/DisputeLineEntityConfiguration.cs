using Cleansia.Core.Domain.Disputes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class DisputeLineEntityConfiguration : AuditableEntityConfiguration<DisputeLine, string>
{
    public override void Configure(EntityTypeBuilder<DisputeLine> builder)
    {
        base.Configure(builder);

        builder.ToTable("DisputeLines");

        builder.Property(l => l.DisputeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(l => l.ServiceId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(l => l.PackageId)
            .HasMaxLength(26);

        builder.HasOne(l => l.Dispute)
            .WithMany(d => d.Lines)
            .HasForeignKey(l => l.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);

        // The point of a child table over a jsonb array: real foreign keys. Restrict, matching the
        // order's own catalog references — a service that a settled dispute names cannot be deleted
        // out from under it, which a bare id inside a JSON document could not promise.
        builder.HasOne<Cleansia.Core.Domain.Services.Service>()
            .WithMany()
            .HasForeignKey(l => l.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Cleansia.Core.Domain.Packages.Package>()
            .WithMany()
            .HasForeignKey(l => l.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per item per dispute. PackageId is nullable, so NULLS NOT DISTINCT is what makes
        // this actually refuse a duplicate standalone service — without it Postgres treats every
        // (serviceId, NULL) pair as distinct and the constraint enforces nothing.
        builder.HasIndex(l => new { l.DisputeId, l.ServiceId, l.PackageId })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
