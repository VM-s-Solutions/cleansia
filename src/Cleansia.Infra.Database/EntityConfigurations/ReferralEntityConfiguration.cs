using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class ReferralEntityConfiguration : AuditableEntityConfiguration<Referral, string>
{
    public override void Configure(EntityTypeBuilder<Referral> builder)
    {
        base.Configure(builder);

        builder.ToTable("Referrals");

        builder.Property(r => r.ReferrerUserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(r => r.ReferredUserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(r => r.ReferralCodeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(r => r.Status)
            .IsRequired();

        builder.Property(r => r.AcceptedOn)
            .IsRequired();

        builder.Property(r => r.FirstQualifyingOrderOn);

        builder.Property(r => r.FirstQualifyingOrderId)
            .HasMaxLength(26)
            .IsRequired(false);

        builder.Property(r => r.PointsAwardedToReferrer);
        builder.Property(r => r.PointsAwardedToReferred);
        builder.Property(r => r.PointsAwardedOn);

        // FKs to two distinct Users — Restrict so neither side is hard-deletable
        // while a referral relationship exists.
        builder.HasOne(r => r.Referrer)
            .WithMany()
            .HasForeignKey(r => r.ReferrerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Referred)
            .WithMany()
            .HasForeignKey(r => r.ReferredUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReferralCode)
            .WithMany()
            .HasForeignKey(r => r.ReferralCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional FK to the qualifying order — Restrict so a completed order
        // can't be hard-deleted while it's recorded as someone's qualifying
        // order. Nullable for Accepted / Expired rows.
        builder.HasOne(r => r.FirstQualifyingOrder)
            .WithMany()
            .HasForeignKey(r => r.FirstQualifyingOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // A user can only be referred once — anti-abuse.
        builder.HasIndex(r => r.ReferredUserId)
            .IsUnique();

        // Paged "my referrals" lookups for the inviter.
        builder.HasIndex(r => r.ReferrerUserId);

        // Status filter for stats and admin views.
        builder.HasIndex(r => r.Status);

        // Daily expiry sweep query: Status=Accepted AND AcceptedOn < cutoff.
        builder.HasIndex(r => new { r.Status, r.AcceptedOn });
    }
}
