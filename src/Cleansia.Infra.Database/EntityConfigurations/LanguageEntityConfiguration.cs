using Cleansia.Core.Domain.Internalization;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class LanguageEntityConfiguration : BaseEntityConfiguration<Language, string>
{
    public override void Configure(EntityTypeBuilder<Language> builder)
    {
        base.Configure(builder);

        builder.Property(l => l.Code)
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(50);
    }
}