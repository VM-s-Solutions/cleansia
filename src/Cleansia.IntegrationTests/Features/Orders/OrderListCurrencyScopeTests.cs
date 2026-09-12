using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Orders.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using SortDefinition = Cleansia.Core.AppServices.Shared.DTOs.Sorting.SortDefinition;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The admin order list across two currencies: a currency filter isolates one, and a price sort with no
/// currency filter is "price within currency" — grouped by currency, ordered by price inside each group
/// — because 150 EUR filed between 3 000 CZK and 100 CZK is not an order of anything. Over real Postgres
/// because both are SQL: the filter is a WHERE term and the grouping is a leading ORDER BY key.
/// </summary>
[Collection("PostgresCollection")]
public class OrderListCurrencyScopeTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CzkId = "currency-czk-olscope";
    private const string EurId = "currency-eur-olscope";
    private const string CountryId = "country-cz-olscope";

    [Fact]
    public async Task A_Currency_Filter_Isolates_That_Currency()
    {
        await TestMethod(
            setup: ReplaceWithAdminSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GetPagedOrders.Request { Filter = EmptyFilter() with { CurrencyId = EurId } }),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                Assert.Equal(2, page.Total);
                Assert.All(page.Data!, row => Assert.Equal("EUR", row.Currency?.Code));

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task A_Price_Sort_With_No_Currency_Filter_Is_Price_Within_Currency()
    {
        await TestMethod(
            setup: ReplaceWithAdminSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GetPagedOrders.Request
                {
                    Filter = EmptyFilter(),
                    Sort = [new SortDefinition("totalPrice", SortDirection.Descending)],
                }),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                var rows = page.Data!.ToList();
                Assert.Equal(5, rows.Count);

                // Grouped: every row of one currency before any row of the other.
                var codes = rows.Select(r => r.Currency!.Code).ToList();
                var boundary = codes.FindIndex(code => code != codes[0]);
                Assert.True(boundary > 0);
                Assert.All(codes.Skip(boundary), code => Assert.NotEqual(codes[0], code));

                // Descending price inside each group.
                foreach (var group in rows.GroupBy(r => r.Currency!.Code))
                {
                    var prices = group.Select(r => r.TotalPrice).ToList();
                    Assert.Equal(prices.OrderByDescending(p => p), prices);
                }

                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task A_Price_Sort_Inside_One_Currency_Is_A_Plain_Price_Sort()
    {
        await TestMethod(
            setup: ReplaceWithAdminSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GetPagedOrders.Request
                {
                    Filter = EmptyFilter() with { CurrencyId = CzkId },
                    Sort = [new SortDefinition("totalPrice", SortDirection.Descending)],
                }),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                var prices = page.Data!.Select(r => r.TotalPrice).ToList();
                Assert.Equal(new[] { 3000m, 1000m, 100m }, prices);

                return Task.CompletedTask;
            });
    }

    private static OrderFilter EmptyFilter() => new(
        Id: null,
        IsActive: null,
        CustomerName: null,
        CustomerEmail: null,
        CustomerPhone: null,
        DisplayOrderNumber: null,
        EmployeeId: null,
        CleaningDateFrom: null,
        CleaningDateTo: null,
        PaymentStatuses: null,
        PaymentTypes: null,
        MinTotalPrice: null,
        MaxTotalPrice: null,
        OrderStatuses: null,
        HasAvailableSpots: null,
        IsUnassigned: null,
        ExcludeEmployeeId: null);

    private static Task ReplaceWithAdminSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            "admin-olscope",
            "admin-olscope@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.IsActive = true;
        czk.Id = CzkId;
        czk.SetAsDefault(true);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.IsActive = true;
        eur.Id = EurId;
        context.Currencies.AddRange(czk, eur);

        context.AddRange(
            NewOrder("order-olscope-czk-a", CzkId, 3000m),
            NewOrder("order-olscope-czk-b", CzkId, 100m),
            NewOrder("order-olscope-czk-c", CzkId, 1000m),
            NewOrder("order-olscope-eur-a", EurId, 150m),
            NewOrder("order-olscope-eur-b", EurId, 40m));

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(string id, string currencyId, decimal totalPrice)
    {
        var order = Order.Create(
            customerName: "List Customer",
            customerEmail: "list-customer@cleansia.test",
            customerPhone: "+420777111333",
            customerAddress: Address.Create("List St 1", "Praha", "12000", CountryId, latitude: 50.0755, longitude: 14.4378),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currencyId,
            paymentStatus: PaymentStatus.Paid);
        order.Id = id;
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }
}
