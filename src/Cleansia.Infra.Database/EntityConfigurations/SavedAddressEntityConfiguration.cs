using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class SavedAddressEntityConfiguration : AuditableEntityConfiguration<SavedAddress, string>
{
    public override void Configure(EntityTypeBuilder<SavedAddress> builder)
    {
        base.Configure(builder);

        builder.Property(s => s.Label)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.IsDefault)
            .HasDefaultValue(false);

        builder.HasOne(s => s.Address)
            .WithMany()
            .HasForeignKey(s => s.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A user can only have one default saved address at a time.
        // Implemented as a filtered unique index (PostgreSQL partial unique index).
        builder.HasIndex(s => s.UserId)
            .HasFilter("\"IsDefault\" = true")
            .IsUnique()
            .HasDatabaseName("IX_SavedAddresses_UserId_Default_Unique");

        // Lookup index for list queries.
        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("IX_SavedAddresses_UserId");
    }
}
