using Cleansia.Core.Domain.ServiceAreas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class ServiceCityEntityConfiguration : AuditableEntityConfiguration<ServiceCity, string>
{
    public override void Configure(EntityTypeBuilder<ServiceCity> builder)
    {
        base.Configure(builder);

        builder.Property(c => c.CountryId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.ZipPrefix)
            .HasMaxLength(20);

        builder.HasOne(c => c.Country)
            .WithMany()
            .HasForeignKey(c => c.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Lookup index for the customer-side city-match validator. We compare
        // case-insensitively in SQL (Lower()), so a regular index on Name is
        // enough — Postgres can use it for ILIKE / LOWER comparisons via the
        // functional index pattern, but for v1 a plain composite index is fine
        // since the matcher loads the candidate set per country (small list).
        builder.HasIndex(c => new { c.CountryId, c.Name });

        builder.HasIndex(c => c.ZipPrefix);
    }
}
