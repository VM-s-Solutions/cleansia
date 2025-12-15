using Cleansia.Core.Domain.ReceiptTemplates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class ReceiptTemplateEntityConfiguration : AuditableEntityConfiguration<ReceiptTemplate, string>
{
    public override void Configure(EntityTypeBuilder<ReceiptTemplate> builder)
    {
        base.Configure(builder);

        builder.ToTable("ReceiptTemplates");

        builder.Property(t => t.TemplateName).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Version).IsRequired();
        builder.Property(t => t.BlobUrl).IsRequired().HasMaxLength(500);
        builder.Property(t => t.IsActive).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(1000);

        builder.HasOne(t => t.Country).WithMany().HasForeignKey(t => t.CountryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Language).WithMany().HasForeignKey(t => t.LanguageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.CountryId, t.LanguageId, t.IsActive }).HasDatabaseName("IX_ReceiptTemplates_Country_Language_Active");
        builder.HasIndex(t => new { t.CountryId, t.LanguageId, t.Version }).IsUnique().HasDatabaseName("IX_ReceiptTemplates_Country_Language_Version");
    }
}
