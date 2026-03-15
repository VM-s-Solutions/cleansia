using Cleansia.Core.Domain.Disputes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class DisputeEvidenceEntityConfiguration : BaseEntityConfiguration<DisputeEvidence, string>
{
    public override void Configure(EntityTypeBuilder<DisputeEvidence> builder)
    {
        base.Configure(builder);

        builder.ToTable("DisputeEvidence");

        builder.Property(de => de.DisputeId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(de => de.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(de => de.FilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(de => de.UploadedBy)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(de => de.UploadedOn)
            .IsRequired();

        // Relationship
        builder.HasOne(de => de.Dispute)
            .WithMany(d => d.Evidence)
            .HasForeignKey(de => de.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(de => de.DisputeId);
        builder.HasIndex(de => de.UploadedOn);
    }
}
