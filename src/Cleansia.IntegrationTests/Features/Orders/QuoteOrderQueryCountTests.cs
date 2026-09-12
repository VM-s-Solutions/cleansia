using System.Data.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Cleansia.IntegrationTests.Features.Orders;

[Collection("PostgresCollection")]
public class QuoteOrderQueryCountTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    /// <summary>
    /// The quote's price, duration and crew come off ONE pass over the catalogue: currency, packages
    /// with their included services, the packages' price rows in that currency, services, the
    /// services' price rows -- five reads -- and the duration is estimated from the rows already in
    /// hand rather than loaded again. Prices are AUTHORED per currency and used as authored: the
    /// total in the named currency is exactly the sum of its rows, converted from nothing.
    /// </summary>
    [Fact]
    public async Task Price_and_duration_share_the_catalogue_reads_in_the_named_currency()
    {
        var counter = new ReadCounter();
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .AddInterceptors(counter)
            .Options;
        await using var context = new CleansiaDbContext(options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new TenantProvider(new HttpContextAccessor()));
        await using var transaction = await context.Database.BeginTransactionAsync();

        var category = ServiceCategory.Create(Guid.NewGuid().ToString(), "Quote", "Query budget");
        var service = Service.Create(category.Id, "Cleaning", "Quote", estimatedTime: 70);
        var package = Package.Create("Bundle", "Quote").AddService(service);
        var currency = Currency.Create("TST", "T", "Test");
        currency.IsActive = true;
        context.AddRange(category, service, package, currency);
        await context.CommitAsync(CancellationToken.None);
        context.AddRange(
            ServicePrice.Create(service.Id, currency.Id, basePrice: 300m, perRoomPrice: 50m),
            PackagePrice.Create(package.Id, currency.Id, 250m));
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var services = new ServiceRepository(context);
        var packages = new PackageRepository(context);
        var waiver = new Mock<IExpressWaiverResolver>();
        waiver.Setup(x => x.ResolveForUserAsync(It.IsAny<string?>(), It.IsAny<DateTime?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExpressWaiver.None("test"));
        var calculator = new OrderPricingCalculator(
            services, packages, new ExtraRepository(context),
            new ServicePriceRepository(context), new PackagePriceRepository(context), new ExtraPriceRepository(context),
            new CurrencyRepository(context), waiver.Object);
        var handler = new QuoteOrder.Handler(calculator,
            Mock.Of<IUserSessionProvider>(), Mock.Of<ILoyaltyService>(),
            Mock.Of<IUserMembershipRepository>(), Mock.Of<ICreditAccountRepository>());

        counter.Count = 0;
        var result = await handler.Handle(new QuoteOrder.Command(
            [service.Id], [package.Id], Rooms: 2, Bathrooms: 1, CurrencyId: currency.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        // 300 + 50 x (2 rooms + 1 bathroom) = 450 for the service, 250 for the package: the rows as authored.
        Assert.Equal(700m, result.Value.TotalPrice);
        Assert.Equal(result.Value.TotalPrice, result.Value.FinalPriceAfterDiscount);
        // Selecting a service directly and in a package intentionally counts it twice.
        Assert.Equal(140, result.Value.EstimatedDurationMinutes);
        Assert.Equal(2, result.Value.RequiredEmployees);
        Assert.Equal(result.Value.TotalPrice, result.Value.Lines!.Sum(line => line.Amount));
        // Currency, packages (+ included services), package prices, services, service prices. No
        // extras were named, so their two reads are skipped, and the duration costs nothing extra.
        Assert.Equal(5, counter.Count);
    }

    private sealed class ReadCounter : DbCommandInterceptor
    {
        public int Count { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }
}
