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
    }
}
