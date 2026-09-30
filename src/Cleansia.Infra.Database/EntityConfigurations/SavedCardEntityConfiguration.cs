using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class SavedCardEntityConfiguration : TenantAuditableEntityConfiguration<SavedCard, string>
{
    public override void Configure(EntityTypeBuilder<SavedCard> builder)
    {
        base.Configure(builder);

        builder.ToTable("SavedCards");

        builder.Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(c => c.CurrencyId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(c => c.StripeCustomerId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(c => c.StripePaymentMethodId)
            .HasMaxLength(64);

        builder.Property(c => c.Brand)
            .HasMaxLength(32);

        builder.Property(c => c.Last4)
            .HasMaxLength(4);

        builder.Property(c => c.ConsentTextVersion)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(c => c.ConsentedOn)
            .IsRequired();

        builder.Property(c => c.ConsentIpAddress)
            .HasMaxLength(45);

        builder.Property(c => c.ConsentDeviceLabel)
            .HasMaxLength(120);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Currency)
            .WithMany()
            .HasForeignKey(c => c.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.UserId, c.CurrencyId })
            .HasDatabaseName("IX_SavedCards_UserId_CurrencyId");
    }
}
