using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class GdprRequestEntityConfiguration : AuditableEntityConfiguration<GdprRequest, string>
{
    public override void Configure(EntityTypeBuilder<GdprRequest> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.UserId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(e => e.RequestType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.Status)
            .IsRequired();

        builder.Property(e => e.ProcessedBy)
            .HasMaxLength(255);

        builder.Property(e => e.Notes)
            .HasMaxLength(1000);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.UserId);
    }
}
