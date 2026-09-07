using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class ReferralCodeEntityConfiguration : AuditableEntityConfiguration<ReferralCode, string>
{
    public override void Configure(EntityTypeBuilder<ReferralCode> builder)
    {
        base.Configure(builder);

        builder.ToTable("ReferralCodes");

        builder.Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(c => c.TimesUsed)
            .IsRequired();

        // 1:1 with User — every user has at most one lifetime code.
        builder.HasOne(c => c.User)
            .WithOne()
            .HasForeignKey<ReferralCode>(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.UserId)
            .IsUnique();

        // Lookup is GetByCodeAsync(code) — codes are tenant-scoped (the
        // global EF filter still applies at query time).
        // NULLS NOT DISTINCT because single-tenant mode is TenantId = null. The odds are long — a
        // generated code collides about once in 481 million attempts — but EnsureCodeForUserAsync's
        // retry-on-collision loop can only see COMMITTED rows, so without this two users can end up
        // holding one code and ProcessOrderCompletedAsync then pays the referral points to whichever
        // of them the unordered lookup returns. Silent, and it credits the wrong person.
        builder.HasIndex(c => new { c.TenantId, c.Code })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
