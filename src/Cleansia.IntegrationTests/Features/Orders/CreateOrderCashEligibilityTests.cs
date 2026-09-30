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
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
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
/// booking e-mail and no benefit reservation.
///
/// <para>Owner ruling 2026-09-28 on top: the customer needs a usable card saved in the booking's currency,
/// holds at most two cash bookings that are open and not yet paid, and owes no operating company an open
/// receivable.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CreateOrderCashEligibilityTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Czk = "currency-czk-cash-rule";
    private const string Eur = "currency-eur-cash-rule";
    private const string TemplateId = "template-cash-rule";
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
                // Owner ruling 2026-09-28: the booking e-mail, and no receipt until the cash is recorded.
                Assert.Single(await context.OutboxMessages.IgnoreQueryFilters()
                    .Where(m => m.QueueName == QueueNames.SendEmail
                        && m.MessageKey == MessageKeys.OrderBookedEmail(result.Value.Id)).ToListAsync());
                Assert.Empty(await context.OutboxMessages.IgnoreQueryFilters()
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
        var slot = ExpressBookingSlot.Next();
        await TestMethod(
            setup: AccountSession,
            arrange: async context =>
            {
                await SeedPlusMemberAsync(context);
                await ExpressBookingSlot.PutMarketInZoneAsync(context, Czechia, slot.TimeZoneId);
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Cash, TwoCleaners, TwoCleanersPrice, slot.CleaningUtc)),
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

    public enum CardOnFile { None, Usable, ExpiredLastMonth, Removed, OtherCurrencyOnly }

    [Theory]
    [InlineData(CardOnFile.None)]
    [InlineData(CardOnFile.ExpiredLastMonth)]
    [InlineData(CardOnFile.Removed)]
    [InlineData(CardOnFile.OtherCurrencyOnly)]
    public async Task A_Signed_In_Customer_Without_A_Usable_Card_In_The_Booking_Currency_Is_Refused_Cash_And_Nothing_Is_Written(
        CardOnFile card)
    {
        await TestMethod(
            setup: AccountSession,
            arrange: context => SeedAsync(context, card),
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Cash, OneCleaner, OneCleanerPrice)),
            assert: (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
                AssertRefusedWithNothingWritten(context, result, BusinessErrorMessage.OrderCashRequiresSavedCard),
            transactional: false);
    }

    [Fact]
    public async Task A_Third_Open_Unpaid_Cash_Booking_Is_Refused_And_Nothing_Is_Written()
    {
        await TestMethod(
            setup: AccountSession,
            arrange: async context =>
            {
                await SeedAsync(context);
                await SeedCustomerOrdersAsync(context, openUnpaidCash: 2);
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Cash, OneCleaner, OneCleanerPrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsFailure);
                var refusal = Assert.Single(Assert.IsAssignableFrom<IValidationResult>(result).Errors);
                Assert.Equal(BusinessErrorMessage.OrderCashOpenBookingsLimitReached, refusal.Message);
                Assert.Equal(nameof(CreateOrder.Command.PaymentType), refusal.Code);
                Assert.Equal(2 + NotCountedOrders, await context.Orders.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    /// <summary>
    /// One open unpaid cash booking beside every order that is not one - cancelled, cash collected, closed,
    /// a recurring occurrence the customer has not confirmed, and a card order - leaves room for a second.
    /// </summary>
    [Fact]
    public async Task Orders_That_Are_Not_Open_Unpaid_Cash_Bookings_Leave_Room_For_Cash()
    {
        await TestMethod(
            setup: AccountSession,
            arrange: async context =>
            {
                await SeedAsync(context);
                await SeedCustomerOrdersAsync(context, openUnpaidCash: 1);
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Cash, OneCleaner, OneCleanerPrice)),
            assert: (CleansiaDbContext _, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Card_Booking_Needs_Neither_A_Saved_Card_Nor_Room_Under_The_Cash_Limit()
    {
        await TestMethod(
            setup: AccountSession,
            arrange: async context =>
            {
                await SeedAsync(context, CardOnFile.None);
                await SeedCustomerOrdersAsync(context, openUnpaidCash: 2);
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Card, OneCleaner, OneCleanerPrice)),
            assert: (CleansiaDbContext _, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Customer_Owing_Another_Company_An_Open_Receivable_Is_Refused_Cash_And_Nothing_Is_Written()
    {
        await TestMethod(
            setup: AccountSession,
            arrange: async context =>
            {
                await SeedAsync(context);
                await SeedReceivableAsync(context, TestTenants.Second, writtenOff: false);
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Cash, OneCleaner, OneCleanerPrice)),
            assert: async (CleansiaDbContext context, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsFailure);
                var refusal = Assert.Single(Assert.IsAssignableFrom<IValidationResult>(result).Errors);
                Assert.Equal(BusinessErrorMessage.OrderCashUnpaidReceivable, refusal.Message);
                Assert.Equal(nameof(CreateOrder.Command.PaymentType), refusal.Code);
                Assert.Equal(1, await context.Orders.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Written_Off_Receivable_Leaves_Cash_Open()
    {
        await TestMethod(
            setup: AccountSession,
            arrange: async context =>
            {
                await SeedAsync(context);
                await SeedReceivableAsync(context, TestTenants.Default, writtenOff: true);
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Cash, OneCleaner, OneCleanerPrice)),
            assert: (CleansiaDbContext _, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Customer_Owing_An_Open_Receivable_Still_Books_By_Card()
    {
        await TestMethod(
            setup: AccountSession,
            arrange: async context =>
            {
                await SeedAsync(context);
                await SeedReceivableAsync(context, TestTenants.Default, writtenOff: false);
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(Command(PaymentType.Card, OneCleaner, OneCleanerPrice)),
            assert: (CleansiaDbContext _, BusinessResult<CreateOrder.Response> result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                return Task.CompletedTask;
            },
            transactional: false);
    }

    private static Task AssertRefusedAndNothingWritten(
        CleansiaDbContext context, BusinessResult<CreateOrder.Response> result)
        => AssertRefusedWithNothingWritten(context, result, BusinessErrorMessage.OrderCashNotAvailable);

    private static async Task AssertRefusedWithNothingWritten(
        CleansiaDbContext context, BusinessResult<CreateOrder.Response> result, string key)
    {
        Assert.True(result.IsFailure);
        var refusal = Assert.Single(Assert.IsAssignableFrom<IValidationResult>(result).Errors);
        Assert.Equal(key, refusal.Message);
        Assert.Equal(nameof(CreateOrder.Command.PaymentType), refusal.Code);

        Assert.Empty(await context.Orders.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await context.OrderStatusHistory.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await context.GuestOrderAccessTokens.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await context.OutboxMessages.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await context.MembershipBenefitUsages.IgnoreQueryFilters().ToListAsync());

        var audit = Assert.Single(await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.False(audit.Success);
        Assert.Equal("customer.order.create", audit.Action);
        Assert.Equal(key, audit.ErrorCode);
        Assert.Null(audit.PayloadJson);
    }

    private const int NotCountedOrders = 5;

    /// <summary>
    /// The customer's other orders, later than the booking under test: the first
    /// <paramref name="openUnpaidCash"/> of the two counted shapes, and always the five that are not counted.
    /// </summary>
    private static async Task SeedCustomerOrdersAsync(CleansiaDbContext context, int openUnpaidCash)
    {
        var start = DateTime.UtcNow.Date.AddDays(5).AddHours(9);
        var counted = new[]
        {
            EarlierOrder(start, PaymentType.Cash, PaymentStatus.Pending, OrderStatus.New),
            EarlierOrder(start.AddDays(1), PaymentType.Cash, PaymentStatus.Pending, OrderStatus.Confirmed, TemplateId)
                .ConfirmByCustomer(DateTime.UtcNow),
        };
        context.Orders.AddRange(counted.Take(openUnpaidCash));
        context.Orders.AddRange(
            EarlierOrder(start.AddDays(2), PaymentType.Cash, PaymentStatus.Pending, OrderStatus.Cancelled),
            EarlierOrder(start.AddDays(3), PaymentType.Cash, PaymentStatus.Paid, OrderStatus.InProgress),
            EarlierOrder(start.AddDays(4), PaymentType.Cash, PaymentStatus.Pending, OrderStatus.Completed),
            EarlierOrder(start.AddDays(5), PaymentType.Cash, PaymentStatus.Pending, OrderStatus.New, TemplateId),
            EarlierOrder(start.AddDays(6), PaymentType.Card, PaymentStatus.Pending, OrderStatus.New));

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    /// <summary>A cash booking the customer cancelled late, and the fee it left owing to <paramref name="tenantId"/>.</summary>
    private static async Task SeedReceivableAsync(CleansiaDbContext context, string tenantId, bool writtenOff)
    {
        var cancelled = EarlierOrder(
            DateTime.UtcNow.Date.AddDays(-2).AddHours(9), PaymentType.Cash, PaymentStatus.Pending, OrderStatus.Cancelled);
        var receivable = Receivable.ForCashCancellationFee(cancelled, 225m);
        if (writtenOff)
        {
            receivable.WriteOff("admin-cash-rule", "Goodwill", DateTimeOffset.UtcNow);
        }

        context.Orders.Add(cancelled);
        context.Receivables.Add(receivable);
        StampUnstampedAdded(context, tenantId);
        await context.CommitAsync(CancellationToken.None);
    }

    private static Order EarlierOrder(
        DateTime cleaningUtc, PaymentType paymentType, PaymentStatus paymentStatus, OrderStatus status,
        string? recurringTemplateId = null)
    {
        var order = Order.Create(
            "Cash Rule Customer", CustomerEmail, "+420777222333",
            Address.Create("Hotovostni 5", City, "11000", Czechia),
            rooms: 2, bathrooms: 1, cleaningUtc, paymentType, OneCleanerPrice, Czk, paymentStatus,
            userId: CustomerUserId, recurringTemplateId: recurringTemplateId);
        order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        return order;
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
        CleaningDate: cleaningDate ?? DateTime.UtcNow.Date.AddDays(3).AddHours(9),
        PaymentType: paymentType,
        CurrencyId: null,
        TotalPrice: totalPrice,
        TermsAccepted: true,
        EarlyPerformanceRequested: true);

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

    private static Task SeedAsync(CleansiaDbContext context) => SeedAsync(context, CardOnFile.Usable);

    private static async Task SeedAsync(CleansiaDbContext context, CardOnFile card)
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

        if (card == CardOnFile.OtherCurrencyOnly)
        {
            var eur = Currency.Create("EUR", "€", "Euro");
            eur.Id = Eur;
            context.Currencies.Add(eur);
        }

        var saved = card switch
        {
            CardOnFile.None => null,
            CardOnFile.ExpiredLastMonth => TestSavedCards.Expiring(CustomerUserId, Czk, DateTime.UtcNow.AddMonths(-1)),
            CardOnFile.OtherCurrencyOnly => TestSavedCards.Usable(CustomerUserId, Eur),
            _ => TestSavedCards.Usable(CustomerUserId, Czk),
        };
        if (card == CardOnFile.Removed)
        {
            saved!.Deactivated(CustomerUserId, DateTimeOffset.UtcNow);
        }

        if (saved is not null)
        {
            context.SavedCards.Add(saved);
        }

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
