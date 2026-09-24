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
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
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
/// Owner ruling 2026-09-24 through the real <c>CreateOrder</c> pipeline on real Postgres: cash only for
/// a signed-in customer whose booking needs one cleaner. The crew comes from the catalogue rows the
/// server prices — 120 minutes is one cleaner, 60 + 61 is two — never from anything the client sends.
/// A refusal writes nothing but its own failure audit row: no order, no status row, no guest token, no
/// receipt message and no benefit reservation.
/// </summary>
[Collection("PostgresCollection")]
public class CreateOrderCashEligibilityTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Czk = "currency-czk-cash-rule";
    private const string Czechia = "country-cz-cash-rule";
    private const string City = "Praha";
    private const string CategoryId = "category-cash-rule";
    private const string TwoHoursId = "service-cash-rule-120";
    private const string OneHourId = "service-cash-rule-60";
    private const string OneHourAndAMinuteId = "service-cash-rule-61";
    private const string CustomerUserId = "user-cash-rule";
    private const string CustomerEmail = "cash-rule@cleansia.test";
    private const decimal TwoHoursPrice = 900m;
    private const decimal OneHourPrice = 500m;
    private const decimal OneHourAndAMinutePrice = 510m;

    private static readonly string[] OneCleaner = [TwoHoursId];
    private static readonly string[] TwoCleaners = [OneHourId, OneHourAndAMinuteId];
    private const decimal OneCleanerPrice = TwoHoursPrice;
    private const decimal TwoCleanersPrice = OneHourPrice + OneHourAndAMinutePrice;

    [Fact]
    public async Task A_Signed_In_Customer_Pays_Cash_For_A_Two_Hour_Job()
    {
        await TestMethod(
            setup: AccountSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Cash, OneCleaner, OneCleanerPrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == result.Value.Id);
                Assert.Equal(PaymentType.Cash, order.PaymentType);
                Assert.Equal(120, order.EstimatedTime);
                Assert.Equal(1, order.RequiredEmployees);
                Assert.Single(await context.OutboxMessages.IgnoreQueryFilters()
                    .Where(m => m.QueueName == QueueNames.GenerateReceipt).ToListAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Signed_In_Customer_Paying_Cash_For_A_Job_Needing_Two_Cleaners_Is_Refused_And_Nothing_Is_Written()
    {
        await TestMethod(
            setup: AccountSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Cash, TwoCleaners, TwoCleanersPrice)),
            assert: AssertRefusedAndNothingWritten,
            transactional: false);
    }

    [Fact]
    public async Task A_Guest_Paying_Cash_For_A_Two_Hour_Job_Is_Refused_And_Nothing_Is_Written()
    {
        await TestMethod(
            setup: GuestSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Cash, OneCleaner, OneCleanerPrice)),
            assert: AssertRefusedAndNothingWritten,
            transactional: false);
    }

    /// <summary>
    /// The handler reserves a Plus member's express slot through a raw INSERT before the factory runs,
    /// so a refusal that came any later than the validator would leave the slot spent. Reaching the cash
    /// link at the WAIVED total proves the waiver was granted: without it the waiver and price links
    /// refuse first.
    /// </summary>
    [Fact]
    public async Task A_Plus_Member_Paying_Cash_For_An_Express_Job_Needing_Two_Cleaners_Keeps_The_Express_Slot()
    {
        await TestMethod(
            setup: AccountSession,
            arrange: SeedPlusMemberAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Cash, TwoCleaners, TwoCleanersPrice, DateTime.UtcNow.AddHours(3))),
            assert: AssertRefusedAndNothingWritten,
            transactional: false);
    }

    [Fact]
    public async Task A_Guest_Pays_By_Card_For_A_Job_Needing_Two_Cleaners()
    {
        await TestMethod(
            setup: GuestSession,
            arrange: SeedAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Card, TwoCleaners, TwoCleanersPrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == result.Value.Id);
                Assert.Equal(PaymentType.Card, order.PaymentType);
                Assert.Equal(121, order.EstimatedTime);
                Assert.Equal(2, order.RequiredEmployees);
            },
            transactional: false);
    }

    private static async Task AssertRefusedAndNothingWritten(
        CleansiaDbContext context, BusinessResult<CreateOrder.Response> result)
    {
        Assert.True(result.IsFailure);
        var refusal = Assert.Single(Assert.IsAssignableFrom<IValidationResult>(result).Errors);
        Assert.Equal(BusinessErrorMessage.OrderCashNotAvailable, refusal.Message);
        Assert.Equal(nameof(CreateOrder.Command.PaymentType), refusal.Code);

        Assert.Empty(await context.Orders.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await context.OrderStatusHistory.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await context.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await context.OutboxMessages.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await context.MembershipBenefitUsages.IgnoreQueryFilters().ToListAsync());

        var audit = Assert.Single(await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.False(audit.Success);
        Assert.Equal("customer.order.create", audit.Action);
        Assert.Equal(BusinessErrorMessage.OrderCashNotAvailable, audit.ErrorCode);
        Assert.Null(audit.PayloadJson);
    }

    private static string Describe(BusinessResult<CreateOrder.Response> result) =>
        string.Join("; ", (result as IValidationResult)?.Errors.Select(e => $"{e.Code}={e.Message}") ?? [result.Error?.Message]);

    private static CreateOrder.Command Command(
        PaymentType paymentType, string[] serviceIds, decimal totalPrice, DateTime? cleaningDate = null) => new(
        CustomerName: "Cash Rule Customer",
        CustomerEmail: CustomerEmail,
        CustomerPhone: "+420777222333",
        CustomerAddress: new AddressDto("Hotovostni 5", City, "11000", Czechia, null),
        SavedAddressId: null,
        SelectedPackageIds: [],
        SelectedServiceIds: serviceIds,
        Rooms: 2,
        Bathrooms: 1,
        Extras: new Dictionary<string, bool>(),
        CleaningDate: cleaningDate ?? DateTime.UtcNow.AddDays(3),
        PaymentType: paymentType,
        CurrencyId: null,
        TotalPrice: totalPrice,
        TermsAccepted: true);

    private static Task AccountSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CustomerUserId,
            CustomerEmail,
            [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
        services.Replace(ServiceDescriptor.Singleton<IOrderChannelProvider>(_ => new OrderChannelProvider(OrderChannel.Mobile)));
        services.Replace(ServiceDescriptor.Scoped<IAddressGeocoder, NoopAddressGeocoder>());
        return Task.CompletedTask;
    }

    /// <summary>The anonymous path: no claim, the scope behaviour sets the tenant from the market. Mobile, so a card order makes no Stripe call.</summary>
    private static Task GuestSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            new TestClaimsPrincipalUser(new ClaimsPrincipal(new ClaimsIdentity())))));
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp =>
            new TenantProvider(sp.GetRequiredService<IHttpContextAccessor>())));
        services.Replace(ServiceDescriptor.Singleton<IOrderChannelProvider>(_ => new OrderChannelProvider(OrderChannel.Mobile)));
        services.Replace(ServiceDescriptor.Scoped<IAddressGeocoder, NoopAddressGeocoder>());
        return Task.CompletedTask;
    }

    private static async Task SeedAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        TestLegalDocuments.Add(context);

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = Czechia;
        context.Countries.Add(country);
        context.CountryConfigurations.Add(CountryConfiguration.Create(Czechia, "CZK", "cs", 0.21m).AssignOperator(TestTenants.Default));
        context.Add(ServiceCity.Create(Czechia, City));

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = Czk;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var category = ServiceCategory.Create("cash-rule", "Cash rule", "Category under test");
        category.Id = CategoryId;
        context.Add(category);

        foreach (var (id, minutes, price) in new[]
                 {
                     (TwoHoursId, 120, TwoHoursPrice),
                     (OneHourId, 60, OneHourPrice),
                     (OneHourAndAMinuteId, 61, OneHourAndAMinutePrice),
                 })
        {
            var service = Service.Create(CategoryId, id, "Under test", minutes);
            service.Id = id;
            context.Add(service);
            context.EmployeePayConfigs.Add(EmployeePayConfig.CreateForService(id, 100m, Czk));
            context.ServicePrices.Add(ServicePrice.Create(id, Czk, price, 0m));
        }

        var user = User.CreateWithPassword(
            CustomerEmail,
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            "Cash",
            "Customer",
            UserProfile.Customer);
        user.Id = CustomerUserId;
        user.ConfirmEmail();
        context.Add(user);

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static async Task SeedPlusMemberAsync(CleansiaDbContext context)
    {
        await SeedAsync(context);

        var plan = MembershipPlan.Create(
            code: "PLUS_CASH_RULE",
            name: "Plus",
            discountPercentage: 0m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            expressUpgradesPerMonth: 1);
        context.Add(plan);
        context.Add(UserMembership.Create(
            CustomerUserId, plan.Id, Czk, "sub_cash_rule", DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(20)));

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private sealed class NoopAddressGeocoder : IAddressGeocoder
    {
        public Task PopulateCoordinatesAsync(Address address, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
