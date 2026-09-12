using Cleansia.Core.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cleansia.Infra.Database.EntityConfigurations;

public class ServicePriceEntityConfiguration : AuditableEntityConfiguration<ServicePrice, string>
{
    public override void Configure(EntityTypeBuilder<ServicePrice> builder)
    {
        base.Configure(builder);

        builder.Property(p => p.BasePrice).IsRequired().HasPrecision(18, 2);
        builder.Property(p => p.PerRoomPrice).IsRequired().HasPrecision(18, 2);

        // PARENT -> PRICE IS CASCADE. Deleting a Service takes its prices with it, because a price
        // row has no meaning without the thing it prices. Getting this backwards would make admin
        // service deletion permanently impossible: DeleteService flushes early and maps ANY
        // foreign-key violation to service.in_use, so with Restrict every service that had
        // a price -- which is every offerable one -- would refuse to delete and report itself in use.
        builder.HasOne(p => p.Service)
            .WithMany()
            .HasForeignKey(p => p.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // CURRENCY -> PRICE IS RESTRICT, the other way round. A currency that something is priced in
        // may not be deleted out from under it. CurrencyRepository.IsInUseAsync gains these three
        // tables in the same change, so the admin gets currency.in_use rather than a raw 500 at commit.
        builder.HasOne(p => p.Currency)
            .WithMany()
            .HasForeignKey(p => p.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        // One price per service per currency.
        //
        // NO TenantId TERM, deliberately, and this is the documented precedent rather than a new
        // judgement: the catalogue is platform config, not tenant data, exactly as
        // PropertySizePresetEntityConfiguration records for (CountryId, Code). It also keeps the index
        // clear of the single-tenant NULL trap -- a unique index containing a nullable TenantId
        // enforces nothing while that column is null, which is production today.
        // -> /architecture/security-rules
        builder.HasIndex(p => new { p.ServiceId, p.CurrencyId })
            .IsUnique()
            .HasDatabaseName("IX_ServicePrices_ServiceId_CurrencyId");
    }
}
