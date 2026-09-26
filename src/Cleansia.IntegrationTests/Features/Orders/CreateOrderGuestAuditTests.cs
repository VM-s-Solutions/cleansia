using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.IntegrationTests.Features.Legal;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Service = Cleansia.Core.Domain.Services.Service;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// ADR-0062 D3/D4 through the real pipeline on real Postgres, as a GUEST: a card checkout leaves ONE
/// <c>customer.order.create</c> row with no user, the host audience filled, <c>isGuest</c>, the terms
/// tick as sent with the version in force, and the standard cancellation window — and no name, contact
/// or address text. A checkout whose submitted total is not the server's leaves one out-of-band failure
/// row carrying the price-mismatch KEY and no payload, and no order. A guest checkout that asserts no
/// tick is refused the same way under <c>consent.terms_not_accepted</c> (owner ruling 2026-09-14): a
/// guest has no account to hold a consent on, so the tick is the only consent there is.
/// </summary>
[Collection("PostgresCollection")]
public class CreateOrderGuestAuditTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Czk = "currency-czk-guest-audit";
    private const string Czechia = "country-cz-guest-audit";
    private const string CategoryId = "category-guest-audit";
    private const string ServiceId = "service-guest-audit";
    private const string PackageId = "package-guest-audit";
    private const string GuestEmail = "guest-audit@cleansia.test";
    private const string GuestStreet = "Guestova 7";
    private const decimal CzkServicePrice = 1000m;
    private const decimal CzkPackagePrice = 500m;
    private const string Ip = "203.0.113.77";
    private const string DeviceLabel = "Chrome/Windows";

    /// <summary>
    /// No claim and no override: the anonymous path, where the scope behaviour sets the tenant from the market.
    /// Mobile, so the guest's card checkout (a guest may not pay cash) mints no Stripe session.
    /// </summary>
    private static Task GuestSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            new TestClaimsPrincipalUser(new ClaimsPrincipal(new ClaimsIdentity())))));
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp =>
            new TenantProvider(sp.GetRequiredService<IHttpContextAccessor>())));
        services.Replace(ServiceDescriptor.Scoped<IRequestMetadataProvider>(_ => new TestRequestMetadataProvider(Ip, DeviceLabel, "device-guest")));
        services.Replace(ServiceDescriptor.Singleton<IOrderChannelProvider>(_ => new OrderChannelProvider(OrderChannel.Mobile)));
        services.Replace(ServiceDescriptor.Scoped<IAddressGeocoder, NoopAddressGeocoder>());
        return Task.CompletedTask;
    }

    private static CreateOrder.Command GuestCommand(decimal totalPrice, bool? termsAccepted) => new(
        CustomerName: "Guest Customer",
        CustomerEmail: GuestEmail,
        CustomerPhone: "+420777333444",
        CustomerAddress: new AddressDto(GuestStreet, "Praha", "11000", Czechia, null),
        SavedAddressId: null,
        SelectedPackageIds: [PackageId],
        SelectedServiceIds: [ServiceId],
        Rooms: 2,
        Bathrooms: 1,
        Extras: new Dictionary<string, bool>(),
        CleaningDate: DateTime.UtcNow.AddDays(3),
        PaymentType: PaymentType.Card,
        CurrencyId: null,
        TotalPrice: totalPrice,
        SpecialInstructions: "gate code 1234",
        TermsAccepted: termsAccepted);

    private static async Task<List<CustomerActionAudit>> CustomerRows(CleansiaDbContext context) =>
        await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync();

    [Fact]
    public async Task A_Guest_Checkout_Leaves_One_Row_With_No_User_The_Host_Audience_The_Tick_And_The_Standard_Window()
    {
        await TestMethod(
            setup: GuestSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(GuestCommand(CzkServicePrice + CzkPackagePrice, termsAccepted: true)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, $"CreateOrder failed with: {string.Join("; ", (result as IValidationResult)?.Errors.Select(e => $"{e.Code}={e.Message}") ?? [result.Error?.Message])}");
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == result.Value.Id);
                Assert.Null(order.UserId);

                // The checkout response is the guest's ONLY synchronous channel for the credential, and
                // without it the success page cannot read back the booking it has just taken payment
                // for. Committed with the order, so the two exist together or not at all.
                Assert.False(string.IsNullOrEmpty(result.Value.GuestAccessToken));
                var accessToken = Assert.Single(
                    await context.GuestOrderAccessTokens.IgnoreQueryFilters().Where(t => t.OrderId == order.Id).ToListAsync());
                Assert.Equal(SecurityTokens.Hash(result.Value.GuestAccessToken!), accessToken.TokenHash);
                Assert.True(accessToken.IsLive(DateTimeOffset.UtcNow));

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.order.create", row.Action);
                Assert.True(row.Success);
                Assert.Null(row.UserId);
                Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
                Assert.Equal(Ip, row.IpAddress);
                Assert.Equal(DeviceLabel, row.DeviceLabel);
                Assert.Equal("Order", row.ResourceType);
                Assert.Equal(order.Id, row.ResourceId);
                Assert.Equal(TestTenants.Default, row.TenantId);

                var payload = JsonDocument.Parse(row.PayloadJson!).RootElement;
                Assert.True(payload.GetProperty("isGuest").GetBoolean());
                Assert.True(payload.GetProperty("termsAccepted").GetBoolean());
                Assert.Equal((await LegalSeed.PlatformWideAsync(context, LegalDocumentType.TermsOfService)).Version, payload.GetProperty("termsVersionAccepted").GetString());
                Assert.Equal(CzkServicePrice + CzkPackagePrice, payload.GetProperty("totalPrice").GetDecimal());
                Assert.Equal("CZK", payload.GetProperty("currencyCode").GetString());
                Assert.Equal(Czechia, payload.GetProperty("countryId").GetString());
                Assert.Equal("card", payload.GetProperty("paymentType").GetString());
                Assert.Equal(order.CustomerAddressId, payload.GetProperty("addressId").GetString());
                Assert.Equal(BookingPolicy.FreeCancellationHours,
                    payload.GetProperty("cancellationPolicyShown").GetProperty("freeHoursForThisCustomer").GetInt32());

                var members = payload.EnumerateObject().Select(p => p.Name).ToList();
                Assert.DoesNotContain("quotedTotalPrice", members);
                Assert.DoesNotContain("preferredEmployeeId", members);
                Assert.DoesNotContain(GuestEmail, row.PayloadJson);
                Assert.DoesNotContain(GuestStreet, row.PayloadJson);
                Assert.DoesNotContain("gate code", row.PayloadJson);
                Assert.Equal(0, await context.AdminActionAudits.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Guest_Checkout_With_A_Stale_Price_Leaves_One_Failure_Row_With_The_Mismatch_Key_And_No_Order()
    {
        await TestMethod(
            setup: GuestSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(GuestCommand(CzkServicePrice + CzkPackagePrice - 1m, termsAccepted: true)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsFailure);
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.TotalPriceNotMatch);
                Assert.Empty(await context.Orders.IgnoreQueryFilters().ToListAsync());

                var row = Assert.Single(await CustomerRows(context));
                Assert.False(row.Success);
                Assert.Equal(BusinessErrorMessage.TotalPriceNotMatch, row.ErrorCode);
                Assert.Equal("customer.order.create", row.Action);
                Assert.Null(row.UserId);
                Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
                Assert.Equal("Order", row.ResourceType);
                Assert.Null(row.ResourceId);
                Assert.Null(row.PayloadJson);
                Assert.Equal(TestTenants.Default, row.TenantId);
            },
            transactional: false);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task A_Guest_Checkout_Without_The_Tick_Is_Refused_And_Leaves_One_Failure_Row_With_The_Key_And_No_Order(bool? termsAccepted)
    {
        await TestMethod(
            setup: GuestSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(GuestCommand(CzkServicePrice + CzkPackagePrice, termsAccepted)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsFailure);
                var refusal = Assert.Single(Assert.IsAssignableFrom<IValidationResult>(result).Errors);
                Assert.Equal(BusinessErrorMessage.TermsNotAccepted, refusal.Message);
                Assert.Equal(nameof(CreateOrder.Command.TermsAccepted), refusal.Code);
                Assert.Empty(await context.Orders.IgnoreQueryFilters().ToListAsync());

                var row = Assert.Single(await CustomerRows(context));
                Assert.False(row.Success);
                Assert.Equal(BusinessErrorMessage.TermsNotAccepted, row.ErrorCode);
                Assert.Equal("customer.order.create", row.Action);
                Assert.Null(row.UserId);
                Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
                Assert.Equal(Ip, row.IpAddress);
                Assert.Equal("Order", row.ResourceType);
                Assert.Null(row.ResourceId);
                Assert.Null(row.PayloadJson);
                Assert.Equal(TestTenants.Default, row.TenantId);
            },
            transactional: false);
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        await LegalSeed.SeedAsync(context);
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = Czechia;
        context.Countries.Add(country);
        context.Add(ServiceCity.Create(Czechia, "Praha"));
        context.CountryConfigurations.Add(CountryConfiguration.Create(Czechia, "CZK", "cs", 0.20m).AssignOperator(TestTenants.Default));

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = Czk;
        czk.IsActive = true;
        czk.SetAsDefault(true);
        context.Currencies.Add(czk);

        var category = ServiceCategory.Create("guest-audit", "Guest audit", "Category under test");
        category.Id = CategoryId;
        context.Add(category);
        var service = Service.Create(CategoryId, "Service", "Under test", 60);
        service.Id = ServiceId;
        context.Add(service);
        var package = Package.Create("Package", "Under test");
        package.Id = PackageId;
        context.Add(package);

        context.EmployeePayConfigs.AddRange(
            EmployeePayConfig.CreateForService(ServiceId, 100m, Czk),
            EmployeePayConfig.CreateForPackage(PackageId, 100m, Czk));
        context.ServicePrices.Add(ServicePrice.Create(ServiceId, Czk, CzkServicePrice, 0m));
        context.PackagePrices.Add(PackagePrice.Create(PackageId, Czk, CzkPackagePrice));

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private sealed class NoopAddressGeocoder : IAddressGeocoder
    {
        public Task PopulateCoordinatesAsync(Address address, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
