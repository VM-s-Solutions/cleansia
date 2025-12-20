using Cleansia.Core.Domain.Disputes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class DisputeMessageEntityConfiguration : IEntityTypeConfiguration<DisputeMessage>
{
    public void Configure(EntityTypeBuilder<DisputeMessage> builder)
    {
        builder.ToTable("DisputeMessages");

        builder.HasKey(dm => dm.Id);

        builder.Property(dm => dm.Id)
            .HasMaxLength(50)
            .ValueGeneratedOnAdd();

        builder.Property(dm => dm.DisputeId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(dm => dm.Message)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(dm => dm.AuthorId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(dm => dm.IsStaffMessage)
            .IsRequired();

        builder.Property(dm => dm.CreatedOn)
            .IsRequired();

        // Relationship
        builder.HasOne(dm => dm.Dispute)
            .WithMany(d => d.Messages)
            .HasForeignKey(dm => dm.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(dm => dm.DisputeId);
        builder.HasIndex(dm => dm.CreatedOn);
    }
}
