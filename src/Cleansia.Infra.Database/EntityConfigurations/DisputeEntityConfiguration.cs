using Cleansia.Core.Domain.Disputes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class DisputeEntityConfiguration : AuditableEntityConfiguration<Dispute, string>
{
    public override void Configure(EntityTypeBuilder<Dispute> builder)
    {
        base.Configure(builder);

        builder.ToTable("Disputes");

        builder.Property(d => d.OrderId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.UserId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(d => d.Reason)
            .IsRequired();

        builder.Property(d => d.Status)
            .IsRequired();

        builder.Property(d => d.ResolutionNotes)
            .HasMaxLength(2000);

        builder.Property(d => d.RefundAmount)
            .HasPrecision(18, 2);

        builder.Property(d => d.ResolvedBy)
            .HasMaxLength(50);

        builder.Property(d => d.StripeDisputeId)
            .HasMaxLength(100);

        // Relationships
        builder.HasOne(d => d.Order)
            .WithMany()
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Messages)
            .WithOne(m => m.Dispute)
            .HasForeignKey(m => m.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Evidence)
            .WithOne(e => e.Dispute)
            .HasForeignKey(e => e.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(d => d.OrderId);
        builder.HasIndex(d => d.UserId);
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.CreatedOn);
    }
}
