using Cleansia.Core.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class EmployeeDocumentEntityConfiguration : AuditableEntityConfiguration<EmployeeDocument, string>
{
    public override void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        base.Configure(builder);

        builder.Property(d => d.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.FilePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.FileSizeBytes)
            .IsRequired();

        builder.Property(d => d.DocumentType)
            .IsRequired();

        builder.Property(d => d.Description)
            .HasMaxLength(500);

        builder.Property(d => d.Version)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(d => d.Status)
            .IsRequired();

        builder.Property(d => d.ReviewNotes)
            .HasMaxLength(500);

        builder
            .HasOne(d => d.Employee)
            .WithMany()
            .HasForeignKey(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(d => d.PreviousVersion)
            .WithMany()
            .HasForeignKey(d => d.PreviousVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.EmployeeId);
        builder.HasIndex(d => d.DocumentType);
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => new { d.EmployeeId, d.DocumentType });
    }
}
