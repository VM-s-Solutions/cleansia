using Cleansia.Core.Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CleanerLegalDocumentAcceptanceConfiguration
    : TenantAuditableEntityConfiguration<CleanerLegalDocumentAcceptance, string>
{
    public override void Configure(EntityTypeBuilder<CleanerLegalDocumentAcceptance> builder)
    {
        base.Configure(builder);

        builder.ToTable("CleanerLegalDocumentAcceptances");

        builder.Property(a => a.EmployeeId)
            .IsRequired()
            .HasMaxLength(26);

        builder.Property(a => a.LegalDocumentTextId)
            .IsRequired()
            .HasMaxLength(26);

        builder.HasOne<LegalDocumentText>()
            .WithMany()
            .HasForeignKey(a => a.LegalDocumentTextId)
            .HasConstraintName("FK_CleanerLegalDocumentAcceptances_LegalDocumentTexts_TextId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.DocumentVersion)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(a => a.AcceptedOn)
            .IsRequired();

        builder.Property(a => a.ClientAudience)
            .IsRequired()
            .HasMaxLength(40);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        builder.Property(a => a.DeviceLabel)
            .HasMaxLength(120);

        builder.Property(a => a.DeviceId)
            .HasMaxLength(64);

        builder.HasIndex(a => new { a.EmployeeId, a.AcceptedOn })
            .HasDatabaseName("IX_CleanerLegalDocumentAcceptances_EmployeeId_AcceptedOn");

        builder.HasIndex(a => new { a.TenantId, a.AcceptedOn })
            .HasDatabaseName("IX_CleanerLegalDocumentAcceptances_TenantId_AcceptedOn");
    }
}
