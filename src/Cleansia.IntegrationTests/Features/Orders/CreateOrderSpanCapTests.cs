using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Service = Cleansia.Core.Domain.Services.Service;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// Drives the REAL <c>CreateOrder</c> route end-to-end over REAL PostgreSQL and asserts the span cap
/// at both edges. The unit suite pins the arithmetic; only this proves the validator's rule is a
/// business error the customer receives rather than the factory's exception, that the over-cap request
/// writes no row, and that the package arm of the sum — a <c>SelectMany</c> over an included-services
/// navigation — actually translates to SQL.
/// </summary>
[Collection("PostgresCollection")]
public class CreateOrderSpanCapTests(PostgresContainerFixture fixture)
    : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-spancap";
    private const string CountryId = "country-cz-spancap";
    private const string CategoryId = "category-spancap";
    private const string LongServiceId = "service-spancap-long";
    private const string OverflowServiceId = "service-spancap-overflow";
    private const string PackagedServiceId = "service-spancap-packaged";
    private const string PackageId = "package-spancap";
    private const string CustomerUserId = "user-cust-spancap";

    private const string CustomerEmail = "span-cap@cleansia.test";
    private const string City = "Praha";

    private const decimal LongServicePrice = 1000m;
    private const decimal OverflowServicePrice = 100m;
    private const decimal PackagedServicePrice = 300m;
    private const decimal PackagePrice = 500m;

    // 1380 + 60 = 1440 min = exactly BookingPolicy.MaxBookableOrderSpanHours. One more minute of
    // catalog is all it takes to cross it.
    private const int LongServiceMinutes = 1380;
    private const int PackagedServiceMinutes = 60;
    private const int OverflowServiceMinutes = 1;

    [Fact]
    public async Task A_Booking_Exactly_At_The_Cap_Is_Created()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedCatalogAndUser,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(BuildCommand(
                    [LongServiceId], [PackageId], LongServicePrice + PackagePrice));
            },
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, $"CreateOrder failed with: {result.Error?.Message}");

                var order = await context.Orders
                    .IgnoreQueryFilters()
                    .SingleAsync(o => o.Id == result.Value.Id);

                Assert.Equal(BookingPolicy.MaxBookableOrderSpanMinutes, order.EstimatedTime);
                Assert.Equal(12, order.RequiredEmployees);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Booking_One_Minute_Over_The_Cap_Is_Refused_And_Writes_Nothing()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedCatalogAndUser,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(BuildCommand(
                    [LongServiceId, OverflowServiceId],
                    [PackageId],
                    LongServicePrice + OverflowServicePrice + PackagePrice));
            },
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.OrderSpanExceedsMaximum);

                Assert.Empty(await context.Orders.IgnoreQueryFilters().ToListAsync());
            },
            transactional: false);
    }

    private static CreateOrder.Command BuildCommand(
        IEnumerable<string> serviceIds, IEnumerable<string> packageIds, decimal totalPrice) => new(
        CustomerName: "Span Cap Customer",
        CustomerEmail: CustomerEmail,
        CustomerPhone: "+420777111444",
        CustomerAddress: new AddressDto("Testovaci 12", City, "11000", CountryId, null),
        SavedAddressId: null,
        SelectedPackageIds: packageIds,
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

        var category = ServiceCategory.Create("spancap", "Span Cap", "Category under test");
        category.Id = CategoryId;
        context.Add(category);

        var longService = Service.Create(
            CategoryId, "Long Service", "Almost the whole cap", LongServicePrice, 0m, LongServiceMinutes);
        longService.Id = LongServiceId;
        context.Add(longService);

        var overflowService = Service.Create(
            CategoryId, "Overflow Service", "One minute past the cap", OverflowServicePrice, 0m, OverflowServiceMinutes);
        overflowService.Id = OverflowServiceId;
        context.Add(overflowService);

        var packagedService = Service.Create(
            CategoryId, "Packaged Service", "Counted through the package", PackagedServicePrice, 0m, PackagedServiceMinutes);
        packagedService.Id = PackagedServiceId;
        context.Add(packagedService);

        var package = Package.Create("Span Cap Package", "Bundle under test", PackagePrice);
        package.Id = PackageId;
        package.AddService(packagedService);
        context.Add(package);

        var user = User.CreateWithPassword(
            CustomerEmail,
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            "Span",
            "Customer",
            UserProfile.Customer);
        user.Id = CustomerUserId;
        user.ConfirmEmail();
        context.Add(user);

        await context.CommitAsync(CancellationToken.None);
    }

    private sealed class NoopAddressGeocoder : IAddressGeocoder
    {
        public Task PopulateCoordinatesAsync(Address address, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
