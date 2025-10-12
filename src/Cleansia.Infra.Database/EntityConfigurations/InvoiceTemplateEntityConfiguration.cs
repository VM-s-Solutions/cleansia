using Cleansia.Core.Domain.InvoiceTemplates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class InvoiceTemplateEntityConfiguration : AuditableEntityConfiguration<InvoiceTemplate, string>
{
    public override void Configure(EntityTypeBuilder<InvoiceTemplate> builder)
    {
        base.Configure(builder);

        builder.ToTable("InvoiceTemplates");

        builder.Property(e => e.TemplateName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Version)
            .IsRequired();

        builder.Property(e => e.BlobUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.HasOne(e => e.Country)
            .WithMany()
            .HasForeignKey(e => e.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Language)
            .WithMany()
            .HasForeignKey(e => e.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CountryId, e.LanguageId, e.IsActive });
    }
}
