using Cleansia.Core.Domain.Emails;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class EmailTranslationEntityConfiguration : AuditableEntityConfiguration<EmailTranslation, string>
{
    public override void Configure(EntityTypeBuilder<EmailTranslation> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.Subject)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Header)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.SubHeader)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.GreetingWord)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Instruction)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.CodeNote)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.Footer)
            .IsRequired()
            .HasMaxLength(1000);
    }
}