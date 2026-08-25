using Cleansia.Core.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class DocumentDeletionRequestEntityConfiguration
    : AuditableEntityConfiguration<DocumentDeletionRequest, string>
{
    public override void Configure(EntityTypeBuilder<DocumentDeletionRequest> builder)
    {
        base.Configure(builder);

        builder.Property(r => r.DocumentId).IsRequired();
        builder.Property(r => r.EmployeeId).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.Status).IsRequired();
        builder.Property(r => r.ReviewNotes).HasMaxLength(1000);

        builder
            .HasOne(r => r.Document)
            .WithMany()
            .HasForeignKey(r => r.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.DocumentId);
        builder.HasIndex(r => r.EmployeeId);

        // The admin queue reads "everything still waiting", so status is what it filters on.
        builder.HasIndex(r => r.Status);
    }
}
