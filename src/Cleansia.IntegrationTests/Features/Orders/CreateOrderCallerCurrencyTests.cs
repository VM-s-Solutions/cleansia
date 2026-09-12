using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
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
/// TWO CURRENCIES LIVE AT ONCE, over real Postgres and through the real validator chain. This is the
/// property the whole programme was building towards and the one nothing could prove until now: every
/// quote and every order used to take the platform default, so a second market could only ever be
/// swapped in for the first, never opened beside it.
///
/// <para>Four currencies are seeded to draw the boundary exactly: CZK (default, priced), EUR (active,
/// priced, not default), HUF (active, unpriced) and PLN (priced, switched off). Only the first two are
/// offerable, and the cases below book in each of them and refuse each of the others.</para>
///
/// <para>Pay configs stay in CZK only. The pay-coverage gate is currency-blind today (T-0701), so a
/// EUR order is admitted on the strength of a CZK rate; that is the next ticket's subject, not this
/// one's, and seeding EUR pay here would hide it.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CreateOrderCallerCurrencyTests(PostgresContainerFixture fixture)
    : BaseIntegrationTest(fixture)
{
    private const string Czk = "currency-czk-caller";
    private const string Eur = "currency-eur-caller";
    private const string Huf = "currency-huf-caller";
    private const string Pln = "currency-pln-caller";
    private const string CountryId = "country-cz-caller";
    private const string CategoryId = "category-caller";
    private const string ServiceId = "service-caller";
    private const string PackageId = "package-caller";
    private const string CustomerUserId = "user-cust-caller";
    private const string CustomerEmail = "caller-currency@cleansia.test";
    private const string City = "Praha";

    private const decimal CzkServicePrice = 1000m;
    private const decimal CzkPackagePrice = 500m;
    private const decimal EurServicePrice = 40m;
    private const decimal EurPackagePrice = 20m;

    [Fact]
    public async Task A_Booking_In_The_Default_Currency_Is_Stamped_With_It()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Czk, CzkServicePrice + CzkPackagePrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, $"CreateOrder failed with: {result.Error?.Message}");
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == result.Value.Id);
                Assert.Equal(Czk, order.CurrencyId);
                Assert.Equal(CzkServicePrice + CzkPackagePrice, order.TotalPrice);
            },
            transactional: false);
    }

    /// <summary>
    /// The one that matters. A second, non-default currency is booked in, priced from ITS rows, and
    /// stamped with ITS id — while CZK stays the default and stays bookable (the case above).
    /// </summary>
    [Fact]
    public async Task A_Booking_In_A_Second_Live_Currency_Is_Priced_From_Its_Own_Rows()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Eur, EurServicePrice + EurPackagePrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, $"CreateOrder failed with: {result.Error?.Message}");
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == result.Value.Id);
                Assert.Equal(Eur, order.CurrencyId);
                Assert.Equal(EurServicePrice + EurPackagePrice, order.TotalPrice);
            },
            transactional: false);
    }

    /// <summary>
    /// The quote and the create agree by construction when the client sends the same currency to both,
    /// which is what every client does: it echoes the quote's <c>currencyId</c> back on create.
    /// </summary>
    [Fact]
    public async Task The_Quote_And_The_Create_Agree_In_A_Second_Currency()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var quote = await mediator.Send(new QuoteOrder.Command(
                    [ServiceId], [PackageId], Rooms: 2, Bathrooms: 1, CurrencyId: Eur));
                Assert.True(quote.IsSuccess, $"QuoteOrder failed with: {quote.Error?.Message}");
                Assert.Equal(Eur, quote.Value.CurrencyId);
                return await mediator.Send(BuildCommand(Eur, quote.Value.TotalPrice));
            },
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, $"CreateOrder failed with: {result.Error?.Message}");
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == result.Value.Id);
                Assert.Equal(Eur, order.CurrencyId);
            },
            transactional: false);
    }

    /// <summary>
    /// Switched on, nothing priced in it. Refused at the door with the currency key, before the
    /// calculator — which would otherwise throw — is reached, and nothing is written.
    /// </summary>
    [Fact]
    public async Task An_Active_But_Unpriced_Currency_Is_Refused_And_Writes_Nothing()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Huf, 1m)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.InvalidCurrency);
                Assert.Empty(await context.Orders.IgnoreQueryFilters().ToListAsync());
            },
            transactional: false);
    }

    /// <summary>Priced, but switched off. The switch wins.</summary>
    [Fact]
    public async Task A_Priced_But_Switched_Off_Currency_Is_Refused()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Pln, CzkServicePrice + CzkPackagePrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.InvalidCurrency);
                Assert.Empty(await context.Orders.IgnoreQueryFilters().ToListAsync());
            },
            transactional: false);
    }

    /// <summary>
    /// A CZK total sent with a EUR currency is not a currency error — EUR is offerable — it is a price
    /// mismatch, because the server re-prices in the currency the caller named.
    /// </summary>
    [Fact]
    public async Task A_Total_From_The_Wrong_Currency_Is_A_Price_Mismatch()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Eur, CzkServicePrice + CzkPackagePrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.TotalPriceNotMatch);
                Assert.DoesNotContain(validation.Errors, e => e.Message == BusinessErrorMessage.InvalidCurrency);
            },
            transactional: false);
    }

    private static CreateOrder.Command BuildCommand(string currencyId, decimal totalPrice) => new(
        CustomerName: "Caller Currency Customer",
        CustomerEmail: CustomerEmail,
        CustomerPhone: "+420777111555",
        CustomerAddress: new AddressDto("Testovaci 12", City, "11000", CountryId, null),
        SavedAddressId: null,
        SelectedPackageIds: [PackageId],
        SelectedServiceIds: [ServiceId],
        Rooms: 2,
        Bathrooms: 1,
        Extras: new Dictionary<string, bool>(),
        CleaningDate: DateTime.UtcNow.AddDays(3),
        PaymentType: PaymentType.Card,
        CurrencyId: currencyId,
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

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        context.Add(ServiceCity.Create(CountryId, City));

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = Czk;
        czk.SetAsDefault(true);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = Eur;
        var huf = Currency.Create("HUF", "Ft", "Forint");
        huf.Id = Huf;
        var pln = Currency.Create("PLN", "zł", "Złoty");
        pln.Id = Pln;
        pln.IsActive = false;
        context.Currencies.AddRange(czk, eur, huf, pln);

        var category = ServiceCategory.Create("caller", "Caller", "Category under test");
        category.Id = CategoryId;
        context.Add(category);

        var service = Service.Create(CategoryId, "Service", "Under test", 60);
        service.Id = ServiceId;
        context.Add(service);

        var package = Package.Create("Package", "Under test");
        package.Id = PackageId;
        context.Add(package);

        // Pay in CZK only -- see the class doc.
        context.EmployeePayConfigs.AddRange(
            EmployeePayConfig.CreateForService(ServiceId, 100m, Czk),
            EmployeePayConfig.CreateForPackage(PackageId, 100m, Czk));

        // CZK and EUR priced; PLN priced but off; HUF on but unpriced.
        context.ServicePrices.AddRange(
            ServicePrice.Create(ServiceId, Czk, CzkServicePrice, 0m),
            ServicePrice.Create(ServiceId, Eur, EurServicePrice, 0m),
            ServicePrice.Create(ServiceId, Pln, 150m, 0m));
        context.PackagePrices.AddRange(
            PackagePrice.Create(PackageId, Czk, CzkPackagePrice),
            PackagePrice.Create(PackageId, Eur, EurPackagePrice),
            PackagePrice.Create(PackageId, Pln, 80m));

        var user = User.CreateWithPassword(
            CustomerEmail,
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            "Caller",
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
