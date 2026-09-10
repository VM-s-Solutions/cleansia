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
    [Theory]
    [InlineData(1)]
    [InlineData(0.04)]
    public async Task Price_and_duration_share_three_catalog_reads(decimal exchangeRate)
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
        var service = Service.Create(category.Id, "Cleaning", "Quote", 300m, 50m, estimatedTime: 70);
        var package = Package.Create("Bundle", "Quote", 250m).AddService(service);
        var currency = Currency.Create("TST", "T", "Test", exchangeRate);
        context.AddRange(category, service, package, currency);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var services = new ServiceRepository(context);
        var packages = new PackageRepository(context);
        var waiver = new Mock<IExpressWaiverResolver>();
        waiver.Setup(x => x.ResolveForUserAsync(It.IsAny<string?>(), It.IsAny<DateTime?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExpressWaiver.None("test"));
        var calculator = new OrderPricingCalculator(services, packages, new ExtraRepository(context),
            new CurrencyRepository(context), waiver.Object);
        var handler = new QuoteOrder.Handler(calculator,
            Mock.Of<IUserSessionProvider>(), Mock.Of<ILoyaltyService>(),
            Mock.Of<ILoyaltyTierConfigRepository>(), Mock.Of<IUserMembershipRepository>(),
            Mock.Of<ICreditAccountRepository>());

        counter.Count = 0;
        var result = await handler.Handle(new QuoteOrder.Command(
            [service.Id], [package.Id], Rooms: 2, Bathrooms: 1, CurrencyId: currency.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(700m * exchangeRate, result.Value.TotalPrice);
        Assert.Equal(result.Value.TotalPrice, result.Value.FinalPriceAfterDiscount);
        // Selecting a service directly and in a package intentionally counts it twice.
        Assert.Equal(140, result.Value.EstimatedDurationMinutes);
        Assert.Equal(2, result.Value.RequiredEmployees);
        Assert.Equal(result.Value.TotalPrice, result.Value.Lines!.Sum(line => line.Amount));
        Assert.Equal(3, counter.Count);
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
