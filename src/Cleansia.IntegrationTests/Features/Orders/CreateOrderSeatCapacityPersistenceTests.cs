using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Service = Cleansia.Core.Domain.Services.Service;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// Drives the REAL <c>CreateOrder</c> handler end-to-end over REAL PostgreSQL and asserts the seat
/// columns the order actually persists. The unit suite pins the rule on the entity; nothing else
/// executes <c>OrderFactory</c>, so nothing else proves the factory hands the domain the policy number
/// rather than a literal — and nothing else proves the ruling is a value change on existing columns.
/// </summary>
[Collection("PostgresCollection")]
public class CreateOrderSeatCapacityPersistenceTests(PostgresContainerFixture fixture)
    : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-seatcap";
    private const string CountryId = "country-cz-seatcap";
    private const string CategoryId = "category-seatcap";
    private const string ShortServiceId = "service-seatcap-short";
    private const string LongServiceId = "service-seatcap-long";
    private const string CustomerUserId = "user-cust-seatcap";

    private const string CustomerEmail = "seat-capacity@cleansia.test";
    private const string City = "Praha";

    private const decimal ShortServicePrice = 1000m;
    private const decimal LongServicePrice = 500m;

    // 60 min → one work unit → a crew of one. 60 + 180 = 240 min → two work units → a crew of two.
    private const int ShortServiceMinutes = 60;
    private const int LongServiceMinutes = 180;

    [Fact]
    public async Task CreateOrder_PersistsSeatCapEqualToTheWorkDerivedCrew()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedCatalogAndUser,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();

                var oneCleaner = await mediator.Send(
                    BuildCommand([ShortServiceId], ShortServicePrice));
                var twoCleaners = await mediator.Send(
                    BuildCommand([ShortServiceId, LongServiceId], ShortServicePrice + LongServicePrice));

                return new CreateAttempt(
                    oneCleaner.IsSuccess ? oneCleaner.Value.Id : null,
                    twoCleaners.IsSuccess ? twoCleaners.Value.Id : null,
                    oneCleaner.Error?.Message ?? twoCleaners.Error?.Message);
            },
            assert: async (CleansiaDbContext context, CreateAttempt attempt) =>
            {
                Assert.False(
                    string.IsNullOrEmpty(attempt.OneCleanerOrderId) || string.IsNullOrEmpty(attempt.TwoCleanerOrderId),
                    $"CreateOrder failed with error: {attempt.ErrorMessage}");

                var oneCleaner = await context.Orders
                    .IgnoreQueryFilters()
                    .SingleAsync(o => o.Id == attempt.OneCleanerOrderId);

                Assert.Equal(ShortServiceMinutes, oneCleaner.EstimatedTime);
                Assert.Equal(1, oneCleaner.RequiredEmployees);
                Assert.Equal(1, oneCleaner.MaxEmployees);

                var twoCleaners = await context.Orders
                    .IgnoreQueryFilters()
                    .SingleAsync(o => o.Id == attempt.TwoCleanerOrderId);

                Assert.Equal(ShortServiceMinutes + LongServiceMinutes, twoCleaners.EstimatedTime);
                Assert.Equal(2, twoCleaners.RequiredEmployees);
                Assert.Equal(2, twoCleaners.MaxEmployees);
            },
            transactional: false);
    }

    private static CreateOrder.Command BuildCommand(IEnumerable<string> serviceIds, decimal totalPrice) => new(
        CustomerName: "Seat Capacity Customer",
        CustomerEmail: CustomerEmail,
        CustomerPhone: "+420777111333",
        CustomerAddress: new AddressDto("Testovaci 12", City, "11000", CountryId, null),
        SavedAddressId: null,
        SelectedPackageIds: [],
        SelectedServiceIds: serviceIds,
        Rooms: 2,
        Bathrooms: 1,
        Extras: new Dictionary<string, bool>(),
        CleaningDate: DateTime.UtcNow.AddDays(3),
        PaymentType: PaymentType.Card,
        CurrencyId: CurrencyId,
        TotalPrice: totalPrice,
        PromoCode: null);

    private static Task ConfigureCustomerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CustomerUserId,
            CustomerEmail,
            [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));

        // Mobile channel short-circuits the Card branch before any Stripe call, so the test needs no gateway.
        services.Replace(ServiceDescriptor.Singleton<IOrderChannelProvider>(
            _ => new OrderChannelProvider(OrderChannel.Mobile)));

        services.Replace(ServiceDescriptor.Scoped<IAddressGeocoder, NoopAddressGeocoder>());

        return Task.CompletedTask;
    }

    private static async Task SeedCatalogAndUser(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        context.Add(ServiceCity.Create(CountryId, City));

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var category = ServiceCategory.Create("seatcap", "Seat Capacity", "Category under test");
        category.Id = CategoryId;
        context.Add(category);

        var shortService = Service.Create(
            CategoryId, "Short Service", "One work unit", ShortServicePrice, 0m, ShortServiceMinutes);
        shortService.Id = ShortServiceId;
        context.Add(shortService);

        var longService = Service.Create(
            CategoryId, "Long Service", "Pushes the order over one work unit", LongServicePrice, 0m, LongServiceMinutes);
        longService.Id = LongServiceId;
        context.Add(longService);

        // Every catalogue entry an order may carry needs its platform-wide pay config: OrderFactory
        // refuses a selection that would quote nothing on a cleaner's board.
        context.EmployeePayConfigs.AddRange(
            EmployeePayConfig.CreateForService(ShortServiceId, 100m, CurrencyId),
            EmployeePayConfig.CreateForService(LongServiceId, 100m, CurrencyId));

        var user = User.CreateWithPassword(
            CustomerEmail,
            Constants.TestUserSession.TestUserPassword,
            "Seat",
            "Customer",
            UserProfile.Customer);
        user.Id = CustomerUserId;
        user.ConfirmEmail();
        context.Add(user);

        await context.CommitAsync(CancellationToken.None);
    }

    private sealed record CreateAttempt(string? OneCleanerOrderId, string? TwoCleanerOrderId, string? ErrorMessage);

    private sealed class NoopAddressGeocoder : IAddressGeocoder
    {
        public Task PopulateCoordinatesAsync(Address address, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
