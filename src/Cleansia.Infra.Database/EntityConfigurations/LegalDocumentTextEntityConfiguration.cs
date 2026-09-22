using Cleansia.Core.Domain.Legal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class LegalDocumentTextEntityConfiguration : BaseEntityConfiguration<LegalDocumentText, string>
{
    public override void Configure(EntityTypeBuilder<LegalDocumentText> builder)
    {
        base.Configure(builder);

        builder.ToTable("LegalDocumentTexts");

        builder.Property(t => t.LegalDocumentId).IsRequired().HasMaxLength(26);
        builder.Property(t => t.Language).IsRequired().HasMaxLength(2);
        builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ContentMarkdown).IsRequired();
        builder.Property(t => t.ContentHash).IsRequired().HasMaxLength(64);

        builder.HasIndex(t => new { t.LegalDocumentId, t.Language }).IsUnique();
    }
}
