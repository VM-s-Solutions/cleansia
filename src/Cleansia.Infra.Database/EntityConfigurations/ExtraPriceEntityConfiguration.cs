using Cleansia.Core.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class ExtraPriceEntityConfiguration : AuditableEntityConfiguration<ExtraPrice, string>
{
    public override void Configure(EntityTypeBuilder<ExtraPrice> builder)
    {
        base.Configure(builder);

        builder.Property(p => p.Price).IsRequired().HasPrecision(18, 2);

        // PARENT -> PRICE IS CASCADE. Deleting a Extra takes its prices with it, because a price
        // row has no meaning without the thing it prices. Getting this backwards would make admin
        // extra deletion permanently impossible: DeleteExtra flushes early and maps ANY
        // foreign-key violation to extra.in_use, so with Restrict every extra that had
        // a price -- which is every offerable one -- would refuse to delete and report itself in use.
        builder.HasOne(p => p.Extra)
            .WithMany()
            .HasForeignKey(p => p.ExtraId)
            .OnDelete(DeleteBehavior.Cascade);

        // CURRENCY -> PRICE IS RESTRICT, the other way round. A currency that something is priced in
        // may not be deleted out from under it. CurrencyRepository.IsInUseAsync gains these three
        // tables in the same change, so the admin gets currency.in_use rather than a raw 500 at commit.
        builder.HasOne(p => p.Currency)
            .WithMany()
            .HasForeignKey(p => p.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // One price per extra per currency.
        //
        // NO TenantId TERM, deliberately, and this is the documented precedent rather than a new
        // judgement: the catalogue is platform config, not tenant data, exactly as
        // PropertySizePresetEntityConfiguration records for (CountryId, Code). It also keeps the index
        // clear of the single-tenant NULL trap -- a unique index containing a nullable TenantId
        // enforces nothing while that column is null, which is production today.
        // -> /architecture/security-rules
        builder.HasIndex(p => new { p.ExtraId, p.CurrencyId })
            .IsUnique()
            .HasDatabaseName("IX_ExtraPrices_ExtraId_CurrencyId");
    }
}
