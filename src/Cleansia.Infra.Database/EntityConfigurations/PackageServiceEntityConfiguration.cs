using Cleansia.Core.Domain.Packages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class PackageServiceEntityConfiguration : IEntityTypeConfiguration<PackageService>
{
    public void Configure(EntityTypeBuilder<PackageService> builder)
    {
        builder.Property(ps => ps.PriceWeight)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasDefaultValue(PackageService.DefaultPriceWeight);

        // Restrict (not the convention Cascade) so deleting a Service that a package bundle includes is
        // rejected at the database; the admin in-use guard maps the resulting 23503 to service.in_use.
        // Name the inverse navigation by string (Service.Packages): the lambda overload
        // .WithMany(s => s.Packages) is rejected because that property is a read-only .ToList() projection,
        // and an unnamed .WithMany() makes EF invent a duplicate shadow FK (ServiceId1). The string overload
        // binds to the existing convention-mapped navigation by metadata name.
        builder.HasOne(ps => ps.Service)
            .WithMany(nameof(Cleansia.Core.Domain.Services.Service.Packages))
            .HasForeignKey(ps => ps.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Package -> PackageService stays Cascade: a package OWNS its included-service composition, so
        // deleting an unused package removes its own bundle rows (the in-use guard never counts these).
        builder.HasOne(ps => ps.Package)
            .WithMany(p => p.IncludedServices)
            .HasForeignKey(ps => ps.PackageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
