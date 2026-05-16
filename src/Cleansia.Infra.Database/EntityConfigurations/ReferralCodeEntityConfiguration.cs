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
        builder.HasIndex(c => new { c.TenantId, c.Code })
            .IsUnique();
    }
}
