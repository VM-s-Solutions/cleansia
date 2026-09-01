using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class SavedAddressEntityConfiguration : AuditableEntityConfiguration<SavedAddress, string>
{
    public override void Configure(EntityTypeBuilder<SavedAddress> builder)
    {
        base.Configure(builder);

        // Per-user unit details. NOT on Address: that row is deduped across
        // every user at the same street, so a flat number there would be read
        // by the neighbours.
        builder.Property(a => a.Floor)
            .HasMaxLength(20);

        builder.Property(a => a.Apartment)
            .HasMaxLength(20);

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

        // A user can only have one ACTIVE default saved address at a time. The filter includes
        // IsActive because soft-delete (Deactivate) leaves IsDefault unchanged on the removed row —
        // without the IsActive predicate a deactivated former-default would still occupy the one-default
        // slot and block choosing a new default.
        builder.HasIndex(s => s.UserId)
            .HasFilter("\"IsDefault\" = true AND \"IsActive\" = true")
            .IsUnique()
            .HasDatabaseName("IX_SavedAddresses_UserId_Default_Unique");

        // Lookup index for list queries.
        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("IX_SavedAddresses_UserId");
    }
}
