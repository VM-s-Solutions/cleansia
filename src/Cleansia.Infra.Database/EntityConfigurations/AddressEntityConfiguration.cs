using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class AddressEntityConfiguration : AuditableEntityConfiguration<Address, string>
{
    public override void Configure(EntityTypeBuilder<Address> builder)
    {
        base.Configure(builder);

        builder.Property(a => a.Street)
            .HasColumnType("citext")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.City)
            .HasColumnType("citext")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.ZipCode)
            .IsRequired()
            .HasMaxLength(20);
    }
}