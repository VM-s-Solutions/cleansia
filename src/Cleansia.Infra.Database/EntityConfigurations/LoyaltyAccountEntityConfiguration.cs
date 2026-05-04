using Cleansia.Core.Domain.Loyalty;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class LoyaltyAccountEntityConfiguration : AuditableEntityConfiguration<LoyaltyAccount, string>
{
    public override void Configure(EntityTypeBuilder<LoyaltyAccount> builder)
    {
        base.Configure(builder);

        builder.ToTable("LoyaltyAccounts");

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(a => a.LifetimePoints)
            .IsRequired();

        builder.Property(a => a.CurrentTier)
            .IsRequired();

        builder.Property(a => a.TierAchievedOn)
            .IsRequired();

        builder.Property(a => a.CompletedBookingsCount)
            .IsRequired();

        // 1:1 with User — unique on UserId
        builder.HasIndex(a => a.UserId)
            .IsUnique();

        builder.HasOne(a => a.User)
            .WithOne()
            .HasForeignKey<LoyaltyAccount>(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // EF must write to the private backing field because Transactions
        // returns a ReadOnlyCollection.
        builder.HasMany(a => a.Transactions)
            .WithOne(t => t.Account)
            .HasForeignKey(t => t.LoyaltyAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.Transactions)
            .HasField("_transactions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
