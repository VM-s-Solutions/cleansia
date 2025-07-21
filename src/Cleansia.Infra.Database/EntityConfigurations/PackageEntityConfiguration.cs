using Cleansia.Core.Domain.Packages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class PackageEntityConfiguration : AuditableEntityConfiguration<Package, string>
{
    public override void Configure(EntityTypeBuilder<Package> builder)
    {
        base.Configure(builder);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.Price)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.HasMany(p => p.IncludedServices)
            .WithMany(s => s.Packages)
            .UsingEntity(j => j.ToTable("PackageServices"));
    }
}