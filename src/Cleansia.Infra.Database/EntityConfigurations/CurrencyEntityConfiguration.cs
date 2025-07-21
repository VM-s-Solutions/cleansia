using Cleansia.Core.Domain.Internalization;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class CurrencyEntityConfiguration : AuditableEntityConfiguration<Currency, string>
{
    public override void Configure(EntityTypeBuilder<Currency> builder)
    {
        base.Configure(builder);

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Symbol)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(c => c.ExchangeRate)
            .IsRequired()
            .HasPrecision(18, 6);
    }
}