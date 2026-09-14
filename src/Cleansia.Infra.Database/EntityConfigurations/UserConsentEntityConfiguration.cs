using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class UserConsentEntityConfiguration : AuditableEntityConfiguration<UserConsent, string>
{
    public override void Configure(EntityTypeBuilder<UserConsent> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.ConsentType)
            .IsRequired();

        builder.Property(e => e.IpAddress)
            .HasMaxLength(45);

        builder.Property(e => e.UserAgent)
            .HasMaxLength(500);

        builder.Property(e => e.DocumentVersion)
            .HasMaxLength(32);

        builder.Property(e => e.LegalDocumentId)
            .HasMaxLength(26);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.LegalDocument)
            .WithMany()
            .HasForeignKey(e => e.LegalDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.UserId, e.ConsentType })
            .IsUnique();
    }
}
