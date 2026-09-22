using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
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
/// TWO MARKETS LIVE AT ONCE, over real Postgres and through the real validator chain. A booking is
/// priced and charged in the currency of the country its service address is in (owner ruling
/// 2026-09-12): a Czech address is a CZK order and a Slovak address is a EUR order, from the same
/// catalogue, with no currency picked by anyone. This is the property the whole programme was building
/// towards and the one nothing could prove until now.
///
/// <para>Four countries are seeded, each configured for its own currency, to draw the boundary
/// exactly: Czechia/CZK (default, priced), Slovakia/EUR (active, priced, not default), Hungary/HUF
/// (active, unpriced) and Poland/PLN (priced, switched off). Only the first two markets are bookable;
/// the cases below book in each of them and refuse each of the others — and refuse a currency named
/// against the address's country, which is what makes the ruling a rule rather than a default.</para>
///
/// <para>Pay is seeded in BOTH live currencies, because the pay-coverage gate asks in the order's
/// currency: a EUR order is admitted only on a EUR rate. The last case removes the EUR rate and proves
/// the gate refuses what the writer could not pay — the agreement the gate exists for.</para>
/// </summary>
[Collection("PostgresCollection")]
public partial class CreateOrderCallerCurrencyTests(PostgresContainerFixture fixture)
    : BaseIntegrationTest(fixture)
{
    private const string Czk = "currency-czk-caller";
    private const string Eur = "currency-eur-caller";
    private const string Huf = "currency-huf-caller";
    private const string Pln = "currency-pln-caller";
    private const string Czechia = "country-cz-caller";
    private const string Slovakia = "country-sk-caller";
    private const string Hungary = "country-hu-caller";
    private const string Poland = "country-pl-caller";
    private const string CategoryId = "category-caller";
    private const string ServiceId = "service-caller";
    private const string PackageId = "package-caller";
    private const string CustomerUserId = "user-cust-caller";
    private const string CustomerEmail = "caller-currency@cleansia.test";

    private const decimal CzkServicePrice = 1000m;
    private const decimal CzkPackagePrice = 500m;
    private const decimal EurServicePrice = 40m;
    private const decimal EurPackagePrice = 20m;

    [Fact]
    public async Task A_Booking_At_A_Czech_Address_Is_Stamped_With_The_Default_Currency()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Czechia, Czk, CzkServicePrice + CzkPackagePrice)),
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
    /// THE RULING. A Slovak address with no currency named at all is a EUR order, priced from the EUR
    /// rows and stamped with the EUR id — while CZK stays the default and a Czech address stays CZK.
    /// </summary>
    [Fact]
    public async Task A_Booking_At_A_Slovak_Address_Is_Priced_And_Stamped_In_Euro_Without_Naming_It()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Slovakia, currencyId: null, EurServicePrice + EurPackagePrice)),
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
    /// The quote and the create agree by construction: the quote is asked with the address's country,
    /// answers in that country's currency, and the client echoes both back on create.
    /// </summary>
    [Fact]
    public async Task The_Quote_With_The_Country_And_The_Create_At_Its_Address_Agree()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var quote = await mediator.Send(new QuoteOrder.Command(
                    [ServiceId], [PackageId], Rooms: 2, Bathrooms: 1, CurrencyId: null, CountryId: Slovakia));
                Assert.True(quote.IsSuccess, $"QuoteOrder failed with: {quote.Error?.Message}");
                Assert.Equal(Eur, quote.Value.CurrencyId);
                Assert.Equal("EUR", quote.Value.CurrencyCode);
                Assert.Equal(EurServicePrice + EurPackagePrice, quote.Value.TotalPrice);
                return await mediator.Send(BuildCommand(Slovakia, quote.Value.CurrencyId, quote.Value.TotalPrice));
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
    /// THE RULING, ENFORCED. The default currency named against a Slovak address is refused with the
    /// currency key and nothing is written: the market is the address's, not the caller's, and a
    /// client that was quoted in CZK before moving the address to Slovakia must re-quote.
    /// </summary>
    [Fact]
    public async Task A_Currency_That_Is_Not_The_Address_Countrys_Is_Refused_And_Writes_Nothing()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Slovakia, Czk, CzkServicePrice + CzkPackagePrice)),
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
    /// Hungary is configured for HUF, which is switched on but has nothing priced in it. Refused at
    /// the door with the currency key, before the calculator — which would otherwise throw — is
    /// reached, and nothing is written.
    /// </summary>
    [Fact]
    public async Task An_Address_In_A_Market_With_An_Unpriced_Currency_Is_Refused_And_Writes_Nothing()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Hungary, currencyId: null, 1m)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.InvalidCurrency);
                Assert.Empty(await context.Orders.IgnoreQueryFilters().ToListAsync());
            },
            transactional: false);
    }

    /// <summary>Poland's PLN is priced, but switched off. The switch wins.</summary>
    [Fact]
    public async Task An_Address_In_A_Market_Whose_Currency_Is_Switched_Off_Is_Refused()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Poland, currencyId: null, 230m)),
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
    /// A CZK total sent to a Slovak address is not a currency error — EUR is the address's currency
    /// and it is offerable — it is a price mismatch, because the server re-prices in EUR.
    /// </summary>
    [Fact]
    public async Task A_Total_From_The_Wrong_Market_Is_A_Price_Mismatch()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Slovakia, Eur, CzkServicePrice + CzkPackagePrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.TotalPriceNotMatch);
                Assert.DoesNotContain(validation.Errors, e => e.Message == BusinessErrorMessage.InvalidCurrency);
            },
            transactional: false);
    }

    /// <summary>
    /// THE GATE AND THE WRITER AGREE. The pay writer reads only rows in the order's currency, so a
    /// EUR order with only CZK rates is refused at the door -- the selection is "invalid" in EUR
    /// exactly as an unpriced one would be -- and nothing is written.
    /// </summary>
    [Fact]
    public async Task A_Booking_In_A_Market_With_No_Pay_Rate_Is_Refused_Rather_Than_Left_Unpayable()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: async context =>
            {
                await SeedAsync(context);
                var eurRates = await context.EmployeePayConfigs.Where(c => c.CurrencyId == Eur).ToListAsync();
                context.EmployeePayConfigs.RemoveRange(eurRates);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Slovakia, currencyId: null, EurServicePrice + EurPackagePrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.False(result.IsSuccess);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.InvalidSelectedServices);
                Assert.Empty(await context.Orders.IgnoreQueryFilters().ToListAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Customer_Booking_Another_Companys_Market_Lands_In_That_Operator()
    {
        await TestMethod(
            setup: ConfigureCustomerSession,
            arrange: SeedWithSlovakiaOperatedBySecondCompanyAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Slovakia, currencyId: null, EurServicePrice + EurPackagePrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == result.Value.Id);
                Assert.Equal(TestTenants.Second, order.TenantId);
                Assert.Equal(CustomerUserId, order.UserId);
                Assert.Equal(Eur, order.CurrencyId);
            },
            transactional: false);
    }

    /// <summary>
    /// A GUEST booking names its market through the address (ADR-0061 D3): the scope behaviour resolves
    /// Slovakia's operator before validation, the order and its address land in that company, and the
    /// agreement rule passes because tenant and currency were read from the same country.
    /// </summary>
    [Fact]
    public async Task A_Guest_Booking_At_A_Slovak_Address_Lands_In_Slovakias_Operating_Company()
    {
        await TestMethod(
            setup: ConfigureGuestSession,
            arrange: SeedWithSlovakiaOperatedBySecondCompanyAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(BuildCommand(Slovakia, currencyId: null, EurServicePrice + EurPackagePrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, $"CreateOrder failed with: {string.Join("; ", (result as IValidationResult)?.Errors.Select(e => $"{e.Code}={e.Message}") ?? [result.Error?.Message])}");
                var order = await context.Orders.IgnoreQueryFilters()
                    .Include(o => o.CustomerAddress)
                    .SingleAsync(o => o.Id == result.Value.Id);
                Assert.Equal(TestTenants.Second, order.TenantId);
                Assert.Equal(TestTenants.Second, order.CustomerAddress.TenantId);
                Assert.Equal(Eur, order.CurrencyId);
                Assert.Empty(await context.Orders.IgnoreQueryFilters().Where(o => o.TenantId == TestTenants.Default).ToListAsync());
            },
            transactional: false);
    }

    /// <summary>
    /// Slovakia handed to the second company, which brings its own EUR pay defaults: pay rates are the
    /// operator's (ADR-0061 D7), and the pay-coverage gate reads them through the filter, so the second
    /// company must hold rates of its own for a booking in its market to be admitted (D12 step 5).
    /// </summary>
    private static async Task SeedWithSlovakiaOperatedBySecondCompanyAsync(CleansiaDbContext context)
    {
        await SeedAsync(context);
        var slovakia = await context.CountryConfigurations.SingleAsync(c => c.CountryId == Slovakia);
        slovakia.AssignOperator(TestTenants.Second);
        foreach (var eurRate in await context.EmployeePayConfigs.IgnoreQueryFilters().Where(c => c.CurrencyId == Eur).ToListAsync())
        {
            eurRate.TenantId = TestTenants.Second;
        }

        await context.CommitAsync(CancellationToken.None);
    }

    private static Task ConfigureGuestSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            new TestClaimsPrincipalUser(new ClaimsPrincipal(new ClaimsIdentity())))));
        // No claim and no override: the anonymous path, where the scope behaviour is what sets the tenant.
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp =>
            new TenantProvider(sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>())));
        services.Replace(ServiceDescriptor.Singleton<IOrderChannelProvider>(
            _ => new OrderChannelProvider(OrderChannel.Mobile)));
        services.Replace(ServiceDescriptor.Scoped<IAddressGeocoder, NoopAddressGeocoder>());
        return Task.CompletedTask;
    }

    private static readonly Dictionary<string, string> CityOf = new()
    {
        [Czechia] = "Praha",
        [Slovakia] = "Bratislava",
        [Hungary] = "Budapest",
        [Poland] = "Warszawa",
    };

    private static CreateOrder.Command BuildCommand(string countryId, string? currencyId, decimal totalPrice) => new(
        CustomerName: "Caller Currency Customer",
        CustomerEmail: CustomerEmail,
        CustomerPhone: "+420777111555",
        CustomerAddress: new AddressDto("Testovaci 12", CityOf[countryId], "11000", countryId, null),
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
        PromoCode: null,
        TermsAccepted: true);

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
        TestLegalDocuments.Add(context);

        // Each country configured for its own currency -- the link the address-to-currency rule reads.
        foreach (var (id, name, iso, code, lang) in new[]
                 {
                     (Czechia, "Czechia", "CZ", "CZK", "cs"),
                     (Slovakia, "Slovakia", "SK", "EUR", "sk"),
                     (Hungary, "Hungary", "HU", "HUF", "hu"),
                     (Poland, "Poland", "PL", "PLN", "pl"),
                 })
        {
            var country = Country.Create(name, iso, iso, isServiced: true);
            country.Id = id;
            context.Countries.Add(country);
            context.Add(ServiceCity.Create(id, CityOf[id]));
            // One operating company serving every seeded country: the operator rule keys on the
            // operator, not the country, so a CZ caller may book the Slovak address (ADR-0061 D6).
            context.CountryConfigurations.Add(CountryConfiguration.Create(id, code, lang, 0.20m).AssignOperator(TestTenants.Default));
        }

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = Czk;
        czk.IsActive = true;
        czk.SetAsDefault(true);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = Eur;
        eur.IsActive = true;
        var huf = Currency.Create("HUF", "Ft", "Forint");
        huf.Id = Huf;
        huf.IsActive = true;
        // Born switched off (Currency.Create), and left that way -- the case that proves the switch
        // wins over the price rows.
        var pln = Currency.Create("PLN", "zł", "Złoty");
        pln.Id = Pln;
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

        // Pay in both live currencies -- see the class doc. The gate asks in the order's currency.
        context.EmployeePayConfigs.AddRange(
            EmployeePayConfig.CreateForService(ServiceId, 100m, Czk),
            EmployeePayConfig.CreateForPackage(PackageId, 100m, Czk),
            EmployeePayConfig.CreateForService(ServiceId, 4m, Eur),
            EmployeePayConfig.CreateForPackage(PackageId, 4m, Eur));

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

        // The guest case seeds where the provider answers null; every other case is a no-op here.
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private sealed class NoopAddressGeocoder : IAddressGeocoder
    {
        public Task PopulateCoordinatesAsync(Address address, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
