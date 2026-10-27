using System.Security.Claims;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.LiveActivities;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Orders.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// Owner ruling 2026-10-26, "I select option B … It can include bookings made before the account existence":
/// a signed-in customer whose address is confirmed sees in My orders every booking made without an account under
/// that address, in any market and whatever the case it was typed in, beside their own. Against real Postgres,
/// through the real pipeline: the listed set is exactly the data subject's set, matched by citext equality on the
/// session's e-mail; a booking under another address, another account's booking that carries the address, and an
/// erased one are not listed; and a cleaner or an administrator signed in with the address lists none.
/// </summary>
[Collection("PostgresCollection")]
public class GuestBookingsInMyOrdersTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string JanaId = "user-jana-listed";
    private const string JanaEmail = "jana.novak@cleansia.test";
    private const string OtherCustomerId = "user-other-listed";
    private const string StaffId = "user-staff-listed";
    private const string CountryId = "country-cz-listed";
    private const string CurrencyId = "cur-czk-listed";
    private const string OwnOrderId = "order-listed-own";
    private const string GuestTypedInCapitalsId = "order-listed-guest-upper";
    private const string GuestInSecondMarketId = "order-listed-guest-second";
    private const string GuestUnderAnotherEmailId = "order-listed-guest-other";
    private const string OtherAccountsOrderId = "order-listed-other-acct";
    private const string ErasedGuestOrderId = "order-listed-anonymised";
    private const string GuestPhone = "+420777000111";
    private const string AccessInstructions = "Code 1234 at the gate.";

    private IUserSessionProvider _session = Customer();
    private readonly Mock<IStripeClient> _stripe = new();

    private string _emailedLink = string.Empty;

    private Task Setup(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => _session));
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        services.Replace(ServiceDescriptor.Singleton(factory.Object));
        _stripe.Setup(s => s.RefundPaymentIntentAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task My_Orders_Lists_And_Counts_The_Accounts_Own_Bookings_And_Those_Made_Without_An_Account_Under_Its_Email()
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => SendAsync(provider, new GetCustomerOrders.Request()),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                Assert.Equal(3, page.Total);
                Assert.Equal(
                    [GuestInSecondMarketId, GuestTypedInCapitalsId, OwnOrderId],
                    page.Data.Select(o => o.Id).Order(StringComparer.Ordinal));

                var own = Assert.Single(page.Data, o => o.Id == OwnOrderId);
                Assert.Null(own.IsGuestBooking);
                Assert.NotEmpty(own.CustomerPhone);
                Assert.All(page.Data.Where(o => o.Id != OwnOrderId), guest =>
                {
                    Assert.True(guest.IsGuestBooking);
                    Assert.Empty(guest.CustomerPhone);
                    Assert.NotEmpty(guest.CustomerName);
                    Assert.NotEmpty(guest.CustomerAddress);
                });
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Listed_Set_Is_The_Data_Subjects_Set_Found_By_Citext_Equality_On_The_Email()
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider =>
            {
                var listed = provider.GetRequiredService<IOrderRepository>().GetQueryableListedForCustomer(JanaId, JanaEmail);
                var subjects = provider.GetRequiredService<CleansiaDbContext>().Orders.IgnoreQueryFilters()
                    .Where(SubjectOrders.Of(JanaId, JanaEmail));
                return Task.FromResult((
                    Sql: listed.ToQueryString(),
                    Listed: listed.Select(o => o.Id).ToList().Order(StringComparer.Ordinal).ToList(),
                    Subjects: subjects.Select(o => o.Id).ToList().Order(StringComparer.Ordinal).ToList()));
            },
            assert: (CleansiaDbContext _, (string Sql, List<string> Listed, List<string> Subjects) read) =>
            {
                Assert.Equal([GuestInSecondMarketId, GuestTypedInCapitalsId, OwnOrderId], read.Listed);
                Assert.Equal(read.Subjects, read.Listed);
                Assert.DoesNotContain("lower(", read.Sql, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("\"CustomerEmail\" = @", read.Sql, StringComparison.Ordinal);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Filter_Applies_To_The_Guest_Bookings_As_To_The_Accounts_Own()
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => SendAsync(provider, new GetCustomerOrders.Request
            {
                Filter = new OrderFilter(
                    Id: null, IsActive: null, CustomerName: null, CustomerEmail: null, CustomerPhone: null,
                    DisplayOrderNumber: null, EmployeeId: null, CleaningDateFrom: null, CleaningDateTo: null,
                    PaymentStatuses: null, PaymentTypes: null, MinTotalPrice: null, MaxTotalPrice: null,
                    OrderStatuses: [OrderStatus.Completed], HasAvailableSpots: null, IsUnassigned: null,
                    ExcludeEmployeeId: null),
            }),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                Assert.Equal(1, page.Total);
                Assert.Equal(GuestInSecondMarketId, Assert.Single(page.Data).Id);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Theory]
    [InlineData(UserProfile.Employee)]
    [InlineData(UserProfile.Administrator)]
    public async Task Staff_Signed_In_To_A_Customer_Host_With_The_Address_List_No_Booking_Made_Under_It(UserProfile profile)
    {
        _session = new TestUserSessionProvider(StaffId, JanaEmail, [new Claim(ClaimTypes.Role, profile.ToString())]);

        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => SendAsync(provider, new GetCustomerOrders.Request()),
            assert: (CleansiaDbContext _, PagedData<OrderListItem> page) =>
            {
                Assert.Equal(0, page.Total);
                Assert.Empty(page.Data);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Theory]
    [InlineData(GuestTypedInCapitalsId)]
    [InlineData(GuestInSecondMarketId)]
    public async Task A_Listed_Guest_Booking_Opens_With_What_Its_Emails_Carried_And_Nothing_The_Household_Wrote(string orderId)
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => SendAsync(provider, new GetOrderDetails.Query(orderId)),
            assert: (CleansiaDbContext _, BusinessResult<OrderItem> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var detail = result.Value!;
                Assert.True(detail.IsGuestBooking);
                Assert.Equal("Jana Nováková", detail.CustomerName);
                Assert.Equal("Vinohradská 12", detail.Address!.Street);
                Assert.NotNull(detail.FreeCancellationHours);
                Assert.Empty(detail.CustomerPhone);
                Assert.Null(detail.AccessInstructions);
                Assert.Null(detail.HasAccessInstructions);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Accounts_Own_Booking_Opens_As_Before()
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => SendAsync(provider, new GetOrderDetails.Query(OwnOrderId)),
            assert: (CleansiaDbContext _, BusinessResult<OrderItem> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Null(result.Value!.IsGuestBooking);
                Assert.Equal(GuestPhone, result.Value.CustomerPhone);
                Assert.Equal(AccessInstructions, result.Value.AccessInstructions);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Theory]
    [InlineData(GuestUnderAnotherEmailId)]
    [InlineData(OtherAccountsOrderId)]
    [InlineData(ErasedGuestOrderId)]
    public async Task A_Booking_The_Account_Is_Not_Listed_For_Is_Not_Found(string orderId)
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => SendAsync(provider, new GetOrderDetails.Query(orderId)),
            assert: (CleansiaDbContext _, BusinessResult<OrderItem> result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Contains(Assert.IsAssignableFrom<IValidationResult>(result).Errors,
                    e => e.Message == BusinessErrorMessage.OrderNotFound);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    /// <summary>
    /// The account prices and cancels the booking it lists on that booking's own guest terms, the e-mailed link
    /// stops working and the cancellation e-mail carries the one that does, and the booking stays a guest booking:
    /// no account is written to it, and no credit, loyalty or receivable row comes of it.
    /// </summary>
    [Fact]
    public async Task A_Listed_Guest_Booking_Is_Priced_And_Cancelled_From_The_Account_On_Its_Own_Terms_And_Stays_A_Guest_Booking()
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: async provider =>
            {
                var preview = await SendAsync(provider, new GetCancellationFeePreview.Query(GuestTypedInCapitalsId));
                var cancel = await SendAsync(provider, new CancelOrder.Command(GuestTypedInCapitalsId, "Booked it twice."));
                var oldLink = await SendAsync(provider, new LookupOrder.Query(_emailedLink));
                return (preview, cancel, oldLink);
            },
            assert: async (CleansiaDbContext context, (BusinessResult<GetCancellationFeePreview.Response> Preview, BusinessResult<CancelOrder.Response> Cancel, BusinessResult<LookupOrder.Response> OldLink) walk) =>
            {
                Assert.True(walk.Preview.IsSuccess, walk.Preview.Error?.Message);
                Assert.True(walk.Cancel.IsSuccess, walk.Cancel.Error?.Message);
                Assert.Equal(walk.Preview.Value!.FeeRate, walk.Cancel.Value!.FeeRate);
                Assert.Equal(walk.Preview.Value.RefundAmount, walk.Cancel.Value.RefundAmount);
                Assert.True(walk.OldLink.IsFailure);

                var order = await context.Orders.IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(o => o.Id == GuestTypedInCapitalsId);
                Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
                Assert.Equal(CancelledBy.Customer, order.CancelledBy);
                Assert.Equal("Booked it twice.", order.CancellationReason);
                Assert.Null(order.UserId);
                Assert.Equal(TestTenants.Second, order.TenantId);

                var refund = Assert.Single(await context.Refunds.IgnoreQueryFilters().AsNoTracking()
                    .Where(r => r.OrderId == GuestTypedInCapitalsId).ToListAsync());
                Assert.Equal($"refund:{GuestTypedInCapitalsId}:cancel", refund.RefundKey);
                Assert.All(await context.GuestOrderAccessTokens.IgnoreQueryFilters().AsNoTracking()
                        .Where(t => t.OrderId == GuestTypedInCapitalsId).ToListAsync(),
                    token => Assert.NotNull(token.RevokedOn));
                Assert.Single(await context.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
                    .Where(m => m.MessageKey == MessageKeys.GuestOrderCancelledEmail(GuestTypedInCapitalsId))
                    .ToListAsync());

                var audit = Assert.Single(await context.CustomerActionAudits.IgnoreQueryFilters().AsNoTracking()
                    .Where(a => a.Action == "customer.order.cancel").ToListAsync());
                Assert.True(audit.Success);
                Assert.Equal(JanaId, audit.UserId);
                Assert.Equal(GuestTypedInCapitalsId, audit.ResourceId);
                Assert.Equal(TestTenants.Second, audit.TenantId);

                Assert.Empty(await context.CreditTransactions.IgnoreQueryFilters().AsNoTracking()
                    .Where(t => t.OrderId == GuestTypedInCapitalsId).ToListAsync());
                Assert.Empty(await context.LoyaltyTransactions.IgnoreQueryFilters().AsNoTracking().ToListAsync());
                Assert.Empty(await context.Receivables.IgnoreQueryFilters().AsNoTracking().ToListAsync());
            },
            transactional: false);
    }

    /// <summary>
    /// Listed, never owned: every other action on one booking keeps the owner pin, so the account is refused on
    /// a booking it lists as on one it does not, and nothing is written. Choosing a preferred cleaner asks for
    /// Cleansia Plus before it reads any order.
    /// </summary>
    [Theory]
    [InlineData("receipt", BusinessErrorMessage.OrderNotFound)]
    [InlineData("photos", BusinessErrorMessage.OrderNotFound)]
    [InlineData("review", BusinessErrorMessage.OrderNotFound)]
    [InlineData("dispute", BusinessErrorMessage.OrderNotFound)]
    [InlineData("no-show", BusinessErrorMessage.OrderNotFound)]
    [InlineData("payment", BusinessErrorMessage.OrderNotFound)]
    [InlineData("resume-checkout", BusinessErrorMessage.OrderNotFound)]
    [InlineData("live-activity", BusinessErrorMessage.OrderNotFound)]
    [InlineData("preferred-cleaner", BusinessErrorMessage.PreferredEmployeeMembershipRequired)]
    public async Task Every_Other_Action_On_A_Listed_Guest_Booking_Is_Refused_And_Writes_Nothing(string action, string refusal)
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: async provider =>
            {
                using var scope = provider.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(RequestFor(action));
            },
            assert: async (CleansiaDbContext context, object? result) =>
            {
                Assert.Contains(refusal, ErrorKeys(result));
                Assert.Empty(await context.OrderReviews.IgnoreQueryFilters().AsNoTracking().ToListAsync());
                Assert.Empty(await context.Disputes.IgnoreQueryFilters().AsNoTracking().ToListAsync());
                Assert.Empty(await context.LiveActivityTokens.IgnoreQueryFilters().AsNoTracking().ToListAsync());
                var booking = await context.Orders.IgnoreQueryFilters().AsNoTracking()
                    .SingleAsync(o => o.Id == GuestTypedInCapitalsId);
                Assert.Equal(OrderStatus.New, booking.CurrentStatus);
                Assert.Null(booking.UserId);
                Assert.Null(booking.PreferredEmployeeId);
            },
            transactional: false);
    }

    /// <summary>The profile's bookings figure stays the account's own: listed bookings are not counted.</summary>
    [Fact]
    public async Task The_Profiles_Bookings_Figure_Counts_The_Accounts_Own_Bookings_Only()
    {
        await TestMethod(
            setup: Setup,
            arrange: SeedAsync,
            act: provider => provider.GetRequiredService<IOrderRepository>()
                .GetCustomerProfileStatsAsync(JanaId, CancellationToken.None),
            assert: (CleansiaDbContext _, CustomerProfileStats stats) =>
            {
                Assert.Equal(1, stats.TotalBookings);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    private static object RequestFor(string action) => action switch
    {
        "receipt" => new DownloadOrderReceipt.Query(GuestInSecondMarketId),
        "photos" => new GetOrderPhotos.Query(GuestInSecondMarketId),
        "review" => new SubmitOrderReview.Command(GuestInSecondMarketId, 5, "Spotless."),
        "dispute" => new CreateDispute.Command(
            GuestInSecondMarketId, DisputeReason.QualityIssue, "The kitchen and the bathroom were not cleaned at all."),
        "no-show" => new CreateDispute.Command(
            GuestTypedInCapitalsId, DisputeReason.ServiceNotProvided, "Nobody came to clean at the booked time today."),
        "payment" => new CreatePaymentIntent.Command(GuestTypedInCapitalsId),
        "resume-checkout" => new ResumeOrderCheckout.Command(GuestTypedInCapitalsId),
        "live-activity" => new RegisterLiveActivityToken.Command("device-listed", "token-listed", GuestTypedInCapitalsId),
        "preferred-cleaner" => new ChoosePreferredCleaner.Command(GuestTypedInCapitalsId, "emp-listed"),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
    };

    private static IEnumerable<string> ErrorKeys(object? result) => result switch
    {
        IValidationResult validation => validation.Errors.Select(e => e.Message),
        BusinessResult { IsFailure: true } failure => [failure.Error!.Message],
        _ => [],
    };

    private static IUserSessionProvider Customer() =>
        new TestUserSessionProvider(JanaId, JanaEmail, [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())]);

    private static async Task<TResponse> SendAsync<TResponse>(IServiceProvider provider, IRequest<TResponse> request)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    /// <summary>
    /// Jana's account and her own booking; two bookings made without an account under her address in the second
    /// operator's market, one typed in capitals a month before the account existed and holding a live e-mailed
    /// link, one completed; and three that are not hers to list: one under another address, another account's
    /// booking carrying hers, and an erased one.
    /// </summary>
    private async Task SeedAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var jana = User.CreateWithPassword(JanaEmail, "Seed-Password-123", "Jana", "Nováková");
        jana.Id = JanaId;
        jana.ConfirmEmail();
        var other = User.CreateWithPassword("petr.svoboda@cleansia.test", "Seed-Password-123", "Petr", "Svoboda");
        other.Id = OtherCustomerId;
        other.ConfirmEmail();
        context.Users.AddRange(jana, other);
        StampUnstampedAdded(context, TestTenants.Default);

        var typedInCapitals = NewOrder(GuestTypedInCapitalsId, userId: null, JanaEmail.ToUpperInvariant(), OrderStatus.New,
            bookedOn: DateTimeOffset.UtcNow.AddDays(-30));
        context.Orders.AddRange(typedInCapitals, NewOrder(GuestInSecondMarketId, userId: null, JanaEmail, OrderStatus.Completed));
        var emailedLink = GuestOrderAccessToken.Issue(GuestTypedInCapitalsId, GuestOrderAccessToken.ExpiryFor(typedInCapitals.CleaningDateTime));
        _emailedLink = emailedLink.RawToken!;
        context.GuestOrderAccessTokens.Add(emailedLink);
        StampUnstampedAdded(context, TestTenants.Second);

        var erased = NewOrder(ErasedGuestOrderId, userId: null, JanaEmail, OrderStatus.Completed);
        erased.AnonymizeCustomerData();
        context.Orders.AddRange(
            NewOrder(OwnOrderId, JanaId, JanaEmail, OrderStatus.New),
            NewOrder(GuestUnderAnotherEmailId, userId: null, "someone.else@cleansia.test", OrderStatus.New),
            NewOrder(OtherAccountsOrderId, OtherCustomerId, JanaEmail, OrderStatus.New),
            erased);
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOrder(
        string id, string? userId, string customerEmail, OrderStatus status, DateTimeOffset? bookedOn = null)
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: customerEmail,
            customerPhone: GuestPhone,
            customerAddress: Address.Create("Vinohradská 12", "Praha", "12000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId,
            accessInstructions: AccessInstructions,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = id;
        order.Created("seed", bookedOn ?? DateTimeOffset.UtcNow.AddDays(-2));
        order.AssignStripePaymentIntentId($"pi_{id}");
        var stamp = (bookedOn ?? DateTimeOffset.UtcNow.AddDays(-2)).AddMinutes(1);
        foreach (var track in status == OrderStatus.New ? [OrderStatus.New] : new[] { OrderStatus.New, status })
        {
            var entry = OrderStatusTrack.Create(track, order);
            entry.Created("seed", stamp);
            order.AddOrderStatus(entry);
            stamp = stamp.AddMinutes(1);
        }

        return order;
    }
}
