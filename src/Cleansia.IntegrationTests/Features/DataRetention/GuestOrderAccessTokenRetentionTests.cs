using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.DataRetention;

/// <summary>
/// A guest booking gathers one link per message it sent, and nothing ever deleted one: a dead link — past
/// its expiry or revoked — opens nothing, since every reader filters to live ones, but its row stayed
/// forever. Through the REAL sweep on real Postgres: the dead rows go, and the one live link survives, so a
/// predicate that deletes too much fails here as surely as an empty step.
/// </summary>
[Collection("PostgresCollection")]
public class GuestOrderAccessTokenRetentionTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-token-ret";
    private const string CurrencyId = "currency-czk-token-ret";
    private const string OrderId = "order-token-ret";

    [Fact]
    public async Task Expired_And_Revoked_Links_Are_Deleted_And_The_Live_One_Survives()
    {
        string liveTokenId = null!;

        await TestMethod(
            setup: services =>
            {
                services.AddScoped<IDataRetentionBackgroundService, DataRetentionBackgroundService>();
                services.Replace(ServiceDescriptor.Singleton(_ => new Mock<IBlobContainerClientFactory>().Object));
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
                country.Id = CountryId;
                context.Countries.Add(country);
                var currency = Currency.Create("CZK", "Kč", "Czech koruna");
                currency.Id = CurrencyId;
                currency.IsActive = true;
                currency.SetAsDefault(true);
                context.Currencies.Add(currency);

                var order = Order.Create(
                    customerName: "Guest Customer", customerEmail: "guest.token-ret@cleansia.test",
                    customerPhone: "+420777111333", customerAddress: Address.Create("Ulice 1", "Praha", "11000", CountryId),
                    rooms: 2, bathrooms: 1, cleaningDateTime: DateTime.UtcNow.AddDays(-5), paymentType: PaymentType.Card,
                    totalPrice: 1250m, currencyId: CurrencyId, paymentStatus: PaymentStatus.Paid);
                order.Id = OrderId;
                order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
                context.Orders.Add(order);

                var live = GuestOrderAccessToken.Issue(OrderId, DateTimeOffset.UtcNow.AddDays(25));
                var expired = GuestOrderAccessToken.Issue(OrderId, DateTimeOffset.UtcNow.AddMinutes(-1));
                var revoked = GuestOrderAccessToken.Issue(OrderId, DateTimeOffset.UtcNow.AddDays(25))
                    .Revoke(DateTimeOffset.UtcNow.AddDays(-1));
                context.GuestOrderAccessTokens.AddRange(live, expired, revoked);
                liveTokenId = live.Id;

                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                await provider.GetRequiredService<IDataRetentionBackgroundService>().RunAllRetentionTasksAsync(CancellationToken.None);
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var survivor = Assert.Single(await context.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(liveTokenId, survivor.Id);
                Assert.Null(survivor.RevokedOn);
            });
    }
}
