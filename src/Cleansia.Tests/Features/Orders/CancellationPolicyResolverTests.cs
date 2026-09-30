using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28: the oops window after booking is 60 minutes on a customer's first booking
/// ever and for an entitled, paid Plus member, and 15 for everyone else — and it is not the plan's
/// free-cancellation HOURS, which is a separate benefit that keeps working as before.
///
/// <para>Runs the REAL <c>UserMembershipRepository</c> and <c>OrderRepository</c> against SQLite:
/// whether a PastDue, paused, expired or trialing enrolment is entitled is a property of the shared
/// predicate, and whether an earlier order makes this one a returning customer's is a property of the
/// query, so a mocked repository would prove only what the mock returned.</para>
/// </summary>
public sealed class CancellationPolicyResolverTests : IDisposable
{
    private const string UserId = "user-oops-1";
    private const string CountryId = "country-oops";
    private const string Email = "oops@cleansia.test";
    private const string Phone = "+420111222333";

    private readonly SqliteConnection _connection;

    public CancellationPolicyResolverTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));
    }

    private async Task SeedAsync(
        int planFreeCancellationHours = 4,
        string? stripeStatus = null,
        DateTime? periodEnd = null,
        DateTime? trialEndsAtUtc = null,
        bool withMembership = true)
    {
        await using var ctx = NewContext();
        await TestTenants.EnsureCreatedWithRegistryAsync(ctx);

        var plan = MembershipPlan.Create(
            code: "PLUS_MONTHLY",
            name: "Plus Monthly",
            discountPercentage: 5m,
            freeCancellationWindowHours: planFreeCancellationHours,
            allowsExpressUpgrade: true);
        ctx.Add(plan);
        ctx.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Add(country);
        var currency = MembershipPricingMockFactory.Czk();
        ctx.Add(currency);
        var user = User.CreateWithPassword(Email, "Password1!", "Oops", "Window", UserProfile.Customer);
        user.Id = UserId;
        ctx.Add(user);

        if (withMembership)
        {
            var now = DateTime.UtcNow;
            var membership = UserMembership.Create(
                UserId, plan.Id, currency.Id, "sub_oops", now.AddDays(-10), periodEnd ?? now.AddDays(20), trialEndsAtUtc);
            if (stripeStatus is not null)
            {
                membership.UpdateFromStripeWebhook(stripeStatus, now.AddDays(-10), periodEnd ?? now.AddDays(20), trialEndsAtUtc);
            }
            ctx.Add(membership);
        }

        await ctx.CommitAsync(CancellationToken.None);
    }

    /// <summary>A committed order, created at <paramref name="createdOn"/>.</summary>
    private async Task<Order> BookedAsync(
        DateTimeOffset createdOn,
        string? userId = UserId,
        string email = Email,
        string phone = Phone,
        bool abandonedCheckout = false,
        PaymentType paymentType = PaymentType.Card,
        PaymentStatus paymentStatus = PaymentStatus.Paid)
    {
        await using var ctx = NewContext();
        var order = NewOrder(createdOn, userId, email, phone, paymentType, paymentStatus);
        if (abandonedCheckout)
        {
            order.UpdatePaymentStatus(PaymentStatus.Failed);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
            order.Cancel(DateTime.UtcNow, CancelledBy.System, 0m, 0m, OrderCancellationReasons.PaymentNotCompleted);
        }

        ctx.Orders.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
        return order;
    }

    private static Order NewOrder(
        DateTimeOffset createdOn,
        string? userId = UserId,
        string email = Email,
        string phone = Phone,
        PaymentType paymentType = PaymentType.Card,
        PaymentStatus paymentStatus = PaymentStatus.Paid)
    {
        var order = Order.Create(
            "Oops Window", email, phone, Address.Create("Oops 1", "Prague", "11000", CountryId), 1, 1,
            DateTime.UtcNow.AddDays(3), paymentType, 1000m, MembershipPricingMockFactory.CzkCurrencyId,
            paymentStatus, userId: userId);
        order.Created("test", createdOn);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }

    private async Task<CancellationPolicy> ResolveAsync(Order order)
    {
        await using var ctx = NewContext();
        return await new CancellationPolicyResolver(new UserMembershipRepository(ctx), new OrderRepository(ctx))
            .ResolveForOrderAsync(order, CancellationToken.None);
    }

    /// <summary>The account's second booking, so only a membership can lengthen its oops window.</summary>
    private async Task<CancellationPolicy> ResolveReturningAsync()
    {
        await BookedAsync(DateTimeOffset.UtcNow.AddDays(-30), email: "earlier@cleansia.test", phone: "+420999000111");
        return await ResolveAsync(await BookedAsync(DateTimeOffset.UtcNow));
    }

    private static void AssertStandard(CancellationPolicy policy)
    {
        Assert.Equal(BookingPolicy.OopsWindowMinutesStandard, policy.OopsWindowMinutes);
        Assert.Equal(OopsWindowRule.Standard, policy.OopsWindowRule);
        Assert.Equal(BookingPolicy.FreeCancellationHours, policy.FreeCancellationHours);
        Assert.Equal(BookingPolicy.PartialCancellationHours, policy.PartialCancellationHours);
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, policy.PartialCancellationFeeRate);
        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, policy.LastMinuteCancellationFeeRate);
    }

    private static void AssertFirstBooking(CancellationPolicy policy)
    {
        Assert.Equal(BookingPolicy.OopsWindowMinutesFirstBooking, policy.OopsWindowMinutes);
        Assert.Equal(OopsWindowRule.FirstBooking, policy.OopsWindowRule);
        Assert.Equal(BookingPolicy.FreeCancellationHours, policy.FreeCancellationHours);
    }

    [Fact]
    public async Task A_Customers_First_Booking_Gets_Sixty_Minutes()
    {
        await SeedAsync(withMembership: false);

        AssertFirstBooking(await ResolveAsync(await BookedAsync(DateTimeOffset.UtcNow)));
    }

    [Fact]
    public async Task A_Customers_Second_Booking_Gets_Fifteen()
    {
        await SeedAsync(withMembership: false);
        await BookedAsync(DateTimeOffset.UtcNow.AddDays(-7));

        AssertStandard(await ResolveAsync(await BookedAsync(DateTimeOffset.UtcNow)));
    }

    /// <summary>
    /// A later booking does not take the first one's sixty minutes away, so the preview, the charge and
    /// the booking evidence all read the same.
    /// </summary>
    [Fact]
    public async Task A_Later_Booking_Does_Not_Take_The_First_Ones_Window_Away()
    {
        await SeedAsync(withMembership: false);
        var first = await BookedAsync(DateTimeOffset.UtcNow.AddDays(-7));
        await BookedAsync(DateTimeOffset.UtcNow);

        AssertFirstBooking(await ResolveAsync(first));
    }

    [Fact]
    public async Task A_Guest_Then_An_Account_With_The_Same_Email_Gets_Fifteen()
    {
        await SeedAsync(withMembership: false);
        await BookedAsync(DateTimeOffset.UtcNow.AddDays(-7), userId: null, phone: "+420555666777");

        AssertStandard(await ResolveAsync(await BookedAsync(DateTimeOffset.UtcNow)));
    }

    [Fact]
    public async Task An_Account_Then_A_Guest_With_The_Same_Phone_Gets_Fifteen()
    {
        await SeedAsync(withMembership: false);
        await BookedAsync(DateTimeOffset.UtcNow.AddDays(-7));

        AssertStandard(await ResolveAsync(
            await BookedAsync(DateTimeOffset.UtcNow, userId: null, email: "other@cleansia.test")));
    }

    [Fact]
    public async Task A_Guests_First_Booking_Gets_Sixty_Minutes_And_Only_Once()
    {
        await SeedAsync(withMembership: false);
        var first = await BookedAsync(DateTimeOffset.UtcNow.AddDays(-7), userId: null, email: "guest@cleansia.test", phone: "+420123123123");
        var second = await BookedAsync(DateTimeOffset.UtcNow, userId: null, email: "guest@cleansia.test", phone: "+420123123123");

        AssertFirstBooking(await ResolveAsync(first));
        AssertStandard(await ResolveAsync(second));
    }

    [Fact]
    public async Task A_Card_Checkout_Abandoned_Unpaid_Is_Not_A_Booking()
    {
        await SeedAsync(withMembership: false);
        await BookedAsync(DateTimeOffset.UtcNow.AddDays(-1), abandonedCheckout: true);

        AssertFirstBooking(await ResolveAsync(await BookedAsync(DateTimeOffset.UtcNow)));
    }

    /// <summary>
    /// A checkout abandoned minutes ago is not cancelled until the stale sweep runs, an hour or more
    /// later. It is no more a booking before the sweep than after, so the rebooking customer is judged
    /// the same throughout.
    /// </summary>
    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    public async Task A_Card_Checkout_Still_Unpaid_And_Not_Yet_Swept_Is_Not_A_Booking(PaymentStatus unpaid)
    {
        await SeedAsync(withMembership: false);
        await BookedAsync(DateTimeOffset.UtcNow.AddMinutes(-20), paymentStatus: unpaid);

        AssertFirstBooking(await ResolveAsync(await BookedAsync(DateTimeOffset.UtcNow)));
    }

    /// <summary>A cash booking is unpaid until the cleaner collects, and is a booking all the same.</summary>
    [Fact]
    public async Task A_Cash_Booking_Not_Yet_Collected_Is_A_Booking()
    {
        await SeedAsync(withMembership: false);
        await BookedAsync(DateTimeOffset.UtcNow.AddDays(-1), paymentType: PaymentType.Cash, paymentStatus: PaymentStatus.Pending);

        AssertStandard(await ResolveAsync(await BookedAsync(DateTimeOffset.UtcNow)));
    }

    [Fact]
    public async Task Somebody_Elses_Earlier_Booking_Does_Not_Count()
    {
        await SeedAsync(withMembership: false);
        await BookedAsync(DateTimeOffset.UtcNow.AddDays(-7), userId: null, email: "stranger@cleansia.test", phone: "+420000999888");

        AssertFirstBooking(await ResolveAsync(await BookedAsync(DateTimeOffset.UtcNow)));
    }

    /// <summary>The booking evidence resolves before the order is committed; nothing earlier exists yet.</summary>
    [Fact]
    public async Task An_Order_Not_Yet_Committed_Is_Judged_On_The_Orders_Before_It()
    {
        await SeedAsync(withMembership: false);

        AssertFirstBooking(await ResolveAsync(NewOrder(DateTimeOffset.UtcNow)));
        await BookedAsync(DateTimeOffset.UtcNow.AddMinutes(-5));
        AssertStandard(await ResolveAsync(NewOrder(DateTimeOffset.UtcNow)));
    }

    [Fact]
    public async Task An_Account_Without_A_Membership_On_A_Second_Booking_Gets_The_Standard_Policy()
    {
        await SeedAsync(withMembership: false);

        AssertStandard(await ResolveReturningAsync());
    }

    [Fact]
    public async Task A_Plus_Members_First_Booking_Records_The_Plus_Rule()
    {
        await SeedAsync();

        var policy = await ResolveAsync(await BookedAsync(DateTimeOffset.UtcNow));

        Assert.Equal(BookingPolicy.OopsWindowMinutesPlus, policy.OopsWindowMinutes);
        Assert.Equal(OopsWindowRule.Plus, policy.OopsWindowRule);
    }

    [Theory]
    [InlineData(0, BookingPolicy.FreeCancellationHours)]
    [InlineData(4, 4)]
    [InlineData(24, 24)]
    public async Task An_Entitled_Member_Gets_Sixty_Minutes_Whatever_The_Plans_Free_Hours(
        int planFreeCancellationHours, int expectedFreeHours)
    {
        await SeedAsync(planFreeCancellationHours);

        var policy = await ResolveReturningAsync();

        Assert.Equal(BookingPolicy.OopsWindowMinutesPlus, policy.OopsWindowMinutes);
        Assert.Equal(OopsWindowRule.Plus, policy.OopsWindowRule);
        Assert.Equal(expectedFreeHours, policy.FreeCancellationHours);
        Assert.Equal(BookingPolicy.PartialCancellationHours, policy.PartialCancellationHours);
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, policy.PartialCancellationFeeRate);
        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, policy.LastMinuteCancellationFeeRate);
    }

    [Theory]
    [InlineData("past_due")]
    [InlineData("paused")]
    [InlineData("canceled")]
    public async Task A_Membership_That_Is_Not_Active_Gets_The_Standard_Policy(string stripeStatus)
    {
        await SeedAsync(stripeStatus: stripeStatus);

        AssertStandard(await ResolveReturningAsync());
    }

    [Fact]
    public async Task An_Expired_Membership_Gets_The_Standard_Policy()
    {
        await SeedAsync(periodEnd: DateTime.UtcNow.AddMinutes(-1));

        AssertStandard(await ResolveReturningAsync());
    }

    [Fact]
    public async Task A_Trialing_Membership_Gets_The_Standard_Policy()
    {
        await SeedAsync(stripeStatus: "trialing", trialEndsAtUtc: DateTime.UtcNow.AddDays(5));

        AssertStandard(await ResolveReturningAsync());
    }

    [Fact]
    public async Task A_Member_Whose_Trial_Has_Ended_Gets_Sixty_Minutes()
    {
        await SeedAsync(trialEndsAtUtc: DateTime.UtcNow.AddDays(-1));

        var policy = await ResolveReturningAsync();

        Assert.Equal(BookingPolicy.OopsWindowMinutesPlus, policy.OopsWindowMinutes);
        Assert.Equal(4, policy.FreeCancellationHours);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
