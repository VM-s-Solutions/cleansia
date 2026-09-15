using Cleansia.Core.Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

/// <summary>
/// Tenantless on purpose: a legal text is platform copy per market, like <c>CountryConfigurations</c>,
/// and a consent row in any operating company points at the same stored text.
/// </summary>
public class LegalDocumentEntityConfiguration : BaseEntityConfiguration<LegalDocument, string>
{
    public override void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        base.Configure(builder);

        builder.ToTable("LegalDocuments");

        builder.Property(d => d.Audience).IsRequired();
        builder.Property(d => d.Type).IsRequired();
        builder.Property(d => d.CountryId).HasMaxLength(26);
        builder.Property(d => d.EffectiveFrom).IsRequired();
        builder.Property(d => d.Version).IsRequired().HasMaxLength(32);
        builder.Property(d => d.Notes).HasMaxLength(500);

        builder.HasOne(d => d.Country)
            .WithMany()
            .HasForeignKey(d => d.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Texts)
            .WithOne(t => t.Document)
            .HasForeignKey(t => t.LegalDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // The seeder's idempotence under six hosts booting at once: two of them can both miss the
        // existence read and both insert; the platform-wide rows carry a null country, so without
        // NULLS NOT DISTINCT the index would let both through.
        builder.HasIndex(d => new { d.Audience, d.Type, d.CountryId, d.Version })
            .IsUnique()
            .AreNullsDistinct(false);

        builder.HasIndex(d => new { d.Audience, d.Type, d.EffectiveFrom });
    }
}
