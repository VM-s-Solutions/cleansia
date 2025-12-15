using Cleansia.Core.Domain.Emails;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class EmailTemplateTranslationEntityConfiguration : AuditableEntityConfiguration<EmailTemplateTranslation, string>
{
    public override void Configure(EntityTypeBuilder<EmailTemplateTranslation> builder)
    {
        base.Configure(builder);

        builder.ToTable("EmailTemplateTranslations");

        builder.Property(e => e.Key).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Value).IsRequired().HasMaxLength(5000);
        builder.Property(e => e.EmailType).IsRequired().HasConversion<int>();

        builder.HasOne(e => e.Language).WithMany().HasForeignKey(e => e.LanguageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.EmailType, e.LanguageId, e.Key }).IsUnique().HasDatabaseName("IX_EmailTemplateTranslations_Type_Language_Key");
    }
}
