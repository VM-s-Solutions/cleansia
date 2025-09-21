using Cleansia.Core.Domain.Internationalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class LanguageEntityConfiguration : BaseEntityConfiguration<Language, string>
{
    public override void Configure(EntityTypeBuilder<Language> builder)
    {
        base.Configure(builder);

        builder.Property(l => l.Code)
            .HasColumnType("citext")
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(l => l.Name)
            .HasColumnType("citext")
            .IsRequired()
            .HasMaxLength(50);
    }
}