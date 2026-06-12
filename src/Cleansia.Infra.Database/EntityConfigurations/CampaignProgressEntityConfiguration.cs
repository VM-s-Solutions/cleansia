using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CampaignProgressEntity = Cleansia.Core.Domain.Messaging.CampaignProgress;

namespace Cleansia.Infra.Database.EntityConfigurations;

/// <summary>
/// EF config for the durable per-campaign resume marker. Like
/// <see cref="ProcessedMessageEntityConfiguration"/> it is a plain
/// <see cref="IEntityTypeConfiguration{TEntity}"/> with no tenant column — the entity is tenant-global by
/// design (a reasoned S8 exception; see <see cref="CampaignProgressEntity"/>). One row per campaign,
/// enforced by the unique index on <c>CampaignId</c>.
/// </summary>
public class CampaignProgressEntityConfiguration : IEntityTypeConfiguration<CampaignProgressEntity>
{
    public void Configure(EntityTypeBuilder<CampaignProgressEntity> builder)
    {
        builder.ToTable("CampaignProgresses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.CampaignId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.LastProcessedUserId)
            .IsRequired(false)
            .HasMaxLength(26);

        builder.Property(e => e.IsComplete)
            .IsRequired();

        // One row per campaign. The store does find-or-insert-then-update; this index makes the invariant
        // load-bearing and a rare first-advance race surfaces as a benign 23505 re-cost (NOT an effect).
        builder.HasIndex(e => e.CampaignId)
            .IsUnique();
    }
}
