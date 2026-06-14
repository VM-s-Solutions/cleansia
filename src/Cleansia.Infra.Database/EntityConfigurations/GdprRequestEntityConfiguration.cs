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

        // The admin GDPR-request list (GetAllGdprRequests) and the retention sweep both order/filter
        // by CreatedOn over the unboundedly growing audit table. Index it so the Article-30 surface
        // sort is an index scan, not a full sort.
        builder.HasIndex(e => e.CreatedOn);
    }
}
