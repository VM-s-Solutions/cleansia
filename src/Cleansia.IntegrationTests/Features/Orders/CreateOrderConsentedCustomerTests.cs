using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.IntegrationTests.Features.Legal;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Constants = Cleansia.TestUtilities.Constants;
using Service = Cleansia.Core.Domain.Services.Service;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The one caller the booking's terms gate excuses (owner ruling 2026-09-14), through the real pipeline
/// on real Postgres: a signed-in customer whose account holds both legal consents, granted and not
/// withdrawn, sees no box on any client, sends nothing, and books — the success row records the tick
/// as not asserted, keyed on the account. The same customer with the privacy consent withdrawn is
/// refused under <c>consent.terms_not_accepted</c> and leaves one failure row keyed on the account.
/// </summary>
[Collection("PostgresCollection")]
public class CreateOrderConsentedCustomerTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Czk = "currency-czk-consented";
    private const string Czechia = "country-cz-consented";
    private const string CategoryId = "category-consented";
    private const string ServiceId = "service-consented";
    private const string CustomerUserId = "user-cust-consented";
    private const string CustomerEmail = "consented@cleansia.test";
    private const decimal CzkServicePrice = 1000m;
    private const string Ip = "203.0.113.88";

    private static Task CustomerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CustomerUserId,
            CustomerEmail,
            [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
        services.Replace(ServiceDescriptor.Scoped<IRequestMetadataProvider>(_ => new TestRequestMetadataProvider(Ip, "Chrome/Windows", "device-consented")));
        services.Replace(ServiceDescriptor.Singleton<IOrderChannelProvider>(_ => new OrderChannelProvider(OrderChannel.Web)));
        services.Replace(ServiceDescriptor.Scoped<IAddressGeocoder, NoopAddressGeocoder>());
        return Task.CompletedTask;
    }

    private static CreateOrder.Command BookingWithoutTheTick() => new(
        CustomerName: "Consented Customer",
        CustomerEmail: CustomerEmail,
        CustomerPhone: "+420777555666",
        CustomerAddress: new AddressDto("Souhlasova 3", "Praha", "11000", Czechia, null),
        SavedAddressId: null,
        SelectedPackageIds: [],
        SelectedServiceIds: [ServiceId],
        Rooms: 2,
        Bathrooms: 1,
        Extras: new Dictionary<string, bool>(),
        CleaningDate: DateTime.UtcNow.AddDays(3),
        PaymentType: PaymentType.Cash,
        CurrencyId: null,
        TotalPrice: CzkServicePrice,
        TermsAccepted: null);

    private static async Task<List<CustomerActionAudit>> CustomerRows(CleansiaDbContext context) =>
        await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync();

    [Fact]
    public async Task A_Customer_Holding_Both_Legal_Consents_Books_Without_The_Tick_And_The_Row_Records_It_As_Not_Asserted()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: context => SeedAsync(context, privacyWithdrawn: false),
            act: async provider => await provider.GetRequiredService<IMediator>().Send(BookingWithoutTheTick()),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, $"CreateOrder failed with: {string.Join("; ", (result as IValidationResult)?.Errors.Select(e => $"{e.Code}={e.Message}") ?? [result.Error?.Message])}");
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == result.Value.Id);
                Assert.Equal(CustomerUserId, order.UserId);

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.order.create", row.Action);
                Assert.True(row.Success);
                Assert.Equal(CustomerUserId, row.UserId);
                Assert.Equal(order.Id, row.ResourceId);
                var payload = JsonDocument.Parse(row.PayloadJson!).RootElement;
                Assert.False(payload.GetProperty("isGuest").GetBoolean());
                Assert.Equal(JsonValueKind.Null, payload.GetProperty("termsAccepted").ValueKind);
                Assert.Equal((await LegalSeed.PlatformWideAsync(context, LegalDocumentType.TermsOfService)).Version, payload.GetProperty("termsVersionAccepted").GetString());
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Customer_Whose_Privacy_Consent_Was_Withdrawn_Is_Refused_Without_The_Tick_And_The_Failure_Row_Is_Keyed_On_The_Account()
    {
        await TestMethod(
            setup: CustomerSession,
            arrange: context => SeedAsync(context, privacyWithdrawn: true),
            act: async provider => await provider.GetRequiredService<IMediator>().Send(BookingWithoutTheTick()),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsFailure);
                var refusal = Assert.Single(Assert.IsAssignableFrom<IValidationResult>(result).Errors);
                Assert.Equal(BusinessErrorMessage.TermsNotAccepted, refusal.Message);
                Assert.Equal(nameof(CreateOrder.Command.TermsAccepted), refusal.Code);
                Assert.Empty(await context.Orders.IgnoreQueryFilters().ToListAsync());

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.order.create", row.Action);
                Assert.False(row.Success);
                Assert.Equal(BusinessErrorMessage.TermsNotAccepted, row.ErrorCode);
                Assert.Equal(CustomerUserId, row.UserId);
                Assert.Null(row.ResourceId);
                Assert.Null(row.PayloadJson);
                Assert.Equal(Ip, row.IpAddress);
                Assert.Equal(TestTenants.Default, row.TenantId);
            },
            transactional: false);
    }

    private static async Task SeedAsync(CleansiaDbContext context, bool privacyWithdrawn)
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

        var category = ServiceCategory.Create("consented", "Consented", "Category under test");
        category.Id = CategoryId;
        context.Add(category);
        var service = Service.Create(CategoryId, "Service", "Under test", 60);
        service.Id = ServiceId;
        context.Add(service);

        context.EmployeePayConfigs.Add(EmployeePayConfig.CreateForService(ServiceId, 100m, Czk));
        context.ServicePrices.Add(ServicePrice.Create(ServiceId, Czk, CzkServicePrice, 0m));

        var customer = User.CreateWithPassword(CustomerEmail, Constants.TestUserSession.TestUserPassword, "Con", "Sented", UserProfile.Customer);
        customer.Id = CustomerUserId;
        customer.ConfirmEmail();
        context.Users.Add(customer);

        var terms = await LegalSeed.PlatformWideAsync(context, LegalDocumentType.TermsOfService);
        var privacy = await LegalSeed.PlatformWideAsync(context, LegalDocumentType.PrivacyPolicy);
        var privacyConsent = UserConsent.Grant(CustomerUserId, ConsentType.PrivacyPolicy, Ip, "Chrome/Windows", privacy.Version, privacy.Id);
        if (privacyWithdrawn)
        {
            privacyConsent.Withdraw();
        }

        context.UserConsents.AddRange(
            UserConsent.Grant(CustomerUserId, ConsentType.TermsOfService, Ip, "Chrome/Windows", terms.Version, terms.Id),
            privacyConsent);

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private sealed class NoopAddressGeocoder : IAddressGeocoder
    {
        public Task PopulateCoordinatesAsync(Address address, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
