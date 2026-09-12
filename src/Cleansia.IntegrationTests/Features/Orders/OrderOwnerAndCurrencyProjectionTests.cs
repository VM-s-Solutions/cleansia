using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// <c>GetOwnerAndCurrencyAsync</c> is a two-column projection into a domain record, against a REAL
/// Postgres: the unit suite mocks the repository, so only this proves EF translates the constructor
/// projection and reads the columns the validator's currency term keys on.
/// </summary>
[Collection("PostgresCollection")]
public class OrderOwnerAndCurrencyProjectionTests(PostgresContainerFixture fixture)
    : BaseIntegrationTest(fixture)
{
    private const string OrderId = "order-owner-currency";
    private const string GuestOrderId = "order-owner-currency-guest";
    private const string OwnerUserId = "user-owner-currency";
    private const string CurrencyId = "currency-owner-currency";

    [Fact]
    public async Task Projects_The_Owner_And_The_Currency_Of_An_Existing_Order()
    {
        await TestMethod(
            arrange: async context => await SeedAsync(context),
            act: provider => provider.GetRequiredService<IOrderRepository>()
                .GetOwnerAndCurrencyAsync(OrderId, CancellationToken.None),
            assert: (_, result) =>
            {
                Assert.NotNull(result);
                Assert.Equal(OwnerUserId, result.UserId);
                Assert.Equal(CurrencyId, result.CurrencyId);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task A_Guest_Order_Projects_A_Null_Owner()
    {
        await TestMethod(
            arrange: async context => await SeedAsync(context),
            act: provider => provider.GetRequiredService<IOrderRepository>()
                .GetOwnerAndCurrencyAsync(GuestOrderId, CancellationToken.None),
            assert: (_, result) =>
            {
                Assert.NotNull(result);
                Assert.Null(result.UserId);
                Assert.Equal(CurrencyId, result.CurrencyId);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task An_Unknown_Order_Is_Null()
    {
        await TestMethod(
            arrange: async context => await SeedAsync(context),
            act: provider => provider.GetRequiredService<IOrderRepository>()
                .GetOwnerAndCurrencyAsync("order-that-does-not-exist", CancellationToken.None),
            assert: (_, result) =>
            {
                Assert.Null(result);
                return Task.CompletedTask;
            });
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = Ulid.NewUlid().ToString();
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kc", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var owner = User.CreateWithPassword("owner-currency@example.com", "12345678Test!", "Own", "Er");
        owner.Id = OwnerUserId;
        context.Users.Add(owner);

        context.Orders.Add(NewOrder(OrderId, country.Id, OwnerUserId));
        context.Orders.Add(NewOrder(GuestOrderId, country.Id, userId: null));

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(string orderId, string countryId, string? userId)
    {
        var order = Order.Create(
            customerName: "Projection Customer",
            customerEmail: $"{orderId}@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", countryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: userId);
        order.Id = orderId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }
}
