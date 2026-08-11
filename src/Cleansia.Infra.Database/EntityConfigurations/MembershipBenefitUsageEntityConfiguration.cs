using Cleansia.Core.Domain.Memberships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class MembershipBenefitUsageEntityConfiguration : AuditableEntityConfiguration<MembershipBenefitUsage, string>
{
    public override void Configure(EntityTypeBuilder<MembershipBenefitUsage> builder)
    {
        base.Configure(builder);

        builder.ToTable("MembershipBenefitUsages");

        builder.Property(u => u.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(u => u.BenefitKind)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(u => u.PeriodKey)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(u => u.SlotOrdinal)
            .IsRequired();

        builder.Property(u => u.OrderId)
            .HasMaxLength(26);

        builder.Property(u => u.UserMembershipId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(u => u.ReservedAtUtc)
            .IsRequired();

        // Restrict throughout: a consumed benefit slot is a billing record, so a hard delete of the
        // user, the order or the enrolment must not silently drop it.
        builder.HasOne(u => u.User)
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Order)
            .WithMany()
            .HasForeignKey(u => u.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.UserMembership)
            .WithMany()
            .HasForeignKey(u => u.UserMembershipId)
            .OnDelete(DeleteBehavior.Restrict);

        // ADR-0035 D3 — the sole arbiter of the reservation race, so NULLS NOT DISTINCT is mandatory,
        // not a style choice: single-tenant mode is TenantId = null, and a nulls-distinct index never
        // fires ON CONFLICT there, turning quota 2 into quota 3+ under concurrency in the platform's
        // default deployment. Filtered to live rows so a release restores capacity while the released
        // row keeps its ordinal for the audit trail.
        builder.HasIndex(u => new { u.TenantId, u.UserId, u.BenefitKind, u.PeriodKey, u.SlotOrdinal })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("\"IsActive\" = TRUE")
            .HasDatabaseName("IX_MembershipBenefitUsages_Slot");

        // Serves the remaining-count read, which is keyed on exactly the quota key — never on
        // UserMembershipId, or the count would reset on re-subscribe (AM-19).
        builder.HasIndex(u => new { u.TenantId, u.UserId, u.BenefitKind, u.PeriodKey })
            .HasDatabaseName("IX_MembershipBenefitUsages_Quota");

        // Serves the orphan reclaim: live slots reserved before a cutoff that never got their order.
        builder.HasIndex(u => u.ReservedAtUtc)
            .HasFilter("\"OrderId\" IS NULL AND \"IsActive\"")
            .HasDatabaseName("IX_MembershipBenefitUsages_Orphans");
    }
}
