using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-24 through the real pipeline on real Postgres, at a fixed clock: the oops window
/// after booking is 15 minutes for a standard account (first-time or returning) and for a guest, and 60
/// for an entitled Plus member. On every route the quote and the charge agree, and both land on a
/// hand-derived amount; a membership that lapses between the quote and the cancel is judged as it
/// stands at the cancel.
///
/// <para>The job is three hours after booking and a cleaner is on it, so outside the oops window every
/// case is the 50% last-minute tier — the Plus plan's 4-hour free window cannot mask it.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CancellationOopsWindowTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CustomerId = "user-oops-window";
    private const string CustomerEmail = "oops-window@cleansia.test";
    private const string OrderId = "order-oops-window";
    private const string PriorOrderId = "order-oops-window-prior";
    private const string CurrencyId = "currency-czk-oops-window";
    private const string CountryId = "country-cz-oops-window";
    private const decimal TotalPrice = 1000m;

    // In the past, so the Cancelled track the cancel appends (stamped at real now) is the latest one.
    private readonly DateTimeOffset _bookedAt = DateTimeOffset.FromUnixTimeSeconds(
        DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds());

    private readonly ManualClock _clock = new(DateTimeOffset.UtcNow);
    private string _guestToken = null!;

    private Func<IServiceCollection, Task> Session(bool guest) => services =>
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => guest
            ? new TestUserSessionProvider()
            : new TestUserSessionProvider(
                CustomerId, CustomerEmail, [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
        services.Replace(ServiceDescriptor.Scoped<IRequestMetadataProvider>(_ =>
            new TestRequestMetadataProvider("203.0.113.60", "oops browser", null)));
        services.Replace(ServiceDescriptor.Scoped<IRefundService>(_ => new SucceedingRefunds()));
        services.Replace(ServiceDescriptor.Singleton<TimeProvider>(_clock));
        return Task.CompletedTask;
    };

    private Func<CleansiaDbContext, Task> Seed(bool guest = false, bool plus = false, bool returning = false) => async db =>
    {
        db.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        db.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        db.Currencies.Add(currency);

        if (!guest)
        {
            var customer = User.CreateWithPassword(CustomerEmail, "Seed-Password-123", "Oops", "Window");
            customer.Id = CustomerId;
            db.Users.Add(customer);
        }

        var cleanerUser = User.CreateWithPassword(
            "cleaner-oops-window@cleansia.test", "Seed-Password-123", "Clean", "Er", UserProfile.Employee);
        var cleaner = Employee.CreateWithUser(cleanerUser);
        db.Users.Add(cleanerUser);
        db.Employees.Add(cleaner);

        var order = NewOrder(OrderId, guest ? null : CustomerId, _bookedAt, OrderStatus.Confirmed);
        order.AssignStripePaymentIntentId("pi_oops_window");
        order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        db.Orders.Add(order);

        if (guest)
        {
            var accessToken = GuestOrderAccessToken.Issue(
                order.Id, GuestOrderAccessToken.ExpiryFor(order.CleaningDateTime));
            _guestToken = accessToken.RawToken!;
            db.GuestOrderAccessTokens.Add(accessToken);
        }

        if (returning)
        {
            db.Orders.Add(NewOrder(PriorOrderId, CustomerId, _bookedAt.AddDays(-30), OrderStatus.Completed));
        }

        if (plus)
        {
            var plan = MembershipPlan.Create(
                code: "PLUS_MONTHLY",
                name: "Plus Monthly",
                discountPercentage: 5m,
                freeCancellationWindowHours: 4,
                allowsExpressUpgrade: true);
            db.MembershipPlans.Add(plan);
            db.UserMemberships.Add(UserMembership.Create(
                CustomerId, plan.Id, CurrencyId, "sub_oops_window",
                DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(20)));
        }

        StampUnstampedAdded(db, TestTenants.Default);
        await db.CommitAsync(CancellationToken.None);
    };

    private static Order NewOrder(string id, string? userId, DateTimeOffset bookedAt, OrderStatus status)
    {
        var order = Order.Create(
            customerName: "Oops Window",
            customerEmail: CustomerEmail,
            customerPhone: "+420777111333",
            customerAddress: Address.Create("Testovaci 15", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: bookedAt.UtcDateTime.AddHours(3),
            paymentType: PaymentType.Card,
            totalPrice: TotalPrice,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = id;
        order.Created("seed", bookedAt);
        var stamp = bookedAt;
        foreach (var track in new[] { OrderStatus.New, status })
        {
            var entry = OrderStatusTrack.Create(track, order);
            entry.Created("seed", stamp);
            order.AddOrderStatus(entry);
            stamp = stamp.AddMinutes(1);
        }

        return order;
    }

    private async Task<GetCancellationFeePreview.Response> PreviewAsync(IServiceProvider provider, bool guest)
    {
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var preview = guest
            ? await mediator.Send(new GetGuestCancellationFeePreview.Query(_guestToken))
            : await mediator.Send(new GetCancellationFeePreview.Query(OrderId));
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        return preview.Value;
    }

    private async Task<CancelOrder.Response> CancelAsync(IServiceProvider provider, bool guest)
    {
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = guest
            ? await mediator.Send(new CancelGuestOrder.Command(_guestToken))
            : await mediator.Send(new CancelOrder.Command(OrderId, Reason: null));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    private static async Task<JsonElement> CancelEvidenceAsync(CleansiaDbContext db)
    {
        var row = Assert.Single(await db.CustomerActionAudits.IgnoreQueryFilters()
            .Where(a => a.Action == "customer.order.cancel" && a.Success)
            .ToListAsync());
        return JsonDocument.Parse(row.PayloadJson!).RootElement;
    }

    public static TheoryData<bool, bool, bool, int, CancellationFeeTier, decimal, int> QuoteAndChargeCases => new()
    {
        // guest, plus, returning, minutes after booking, tier, fee, oops minutes applied
        { false, true, false, 59, CancellationFeeTier.FreeOopsWindow, 0m, 60 },
        { false, true, false, 61, CancellationFeeTier.LastMinute, 500m, 60 },
        { false, false, false, 14, CancellationFeeTier.FreeOopsWindow, 0m, 15 },
        { false, false, false, 16, CancellationFeeTier.LastMinute, 500m, 15 },
        { false, false, true, 14, CancellationFeeTier.FreeOopsWindow, 0m, 15 },
        { false, false, true, 16, CancellationFeeTier.LastMinute, 500m, 15 },
        { true, false, false, 14, CancellationFeeTier.FreeOopsWindow, 0m, 15 },
        { true, false, false, 16, CancellationFeeTier.LastMinute, 500m, 15 },
    };

    [Theory]
    [MemberData(nameof(QuoteAndChargeCases))]
    public async Task The_Quote_And_The_Charge_Agree_On_The_Customers_Own_Oops_Window(
        bool guest, bool plus, bool returning, int minutesAfterBooking,
        CancellationFeeTier expectedTier, decimal expectedFee, int expectedOopsMinutes)
    {
        var expectedRefund = TotalPrice - expectedFee;
        await TestMethod(
            setup: Session(guest),
            arrange: Seed(guest, plus, returning),
            act: async provider =>
            {
                _clock.Set(_bookedAt.AddMinutes(minutesAfterBooking));
                var preview = await PreviewAsync(provider, guest);
                var charge = await CancelAsync(provider, guest);
                return new QuoteAndCharge(preview, charge);
            },
            assert: async (CleansiaDbContext db, QuoteAndCharge result) =>
            {
                Assert.Equal(expectedTier, result.Preview.Tier);
                Assert.Equal(expectedFee, result.Preview.FeeAmount);
                Assert.Equal(expectedRefund, result.Preview.RefundAmount);
                Assert.Equal(expectedOopsMinutes, result.Preview.OopsWindowMinutes);

                Assert.Equal(result.Preview.FeeRate, result.Charge.FeeRate);
                Assert.Equal(expectedRefund, result.Charge.RefundAmount);

                var order = await db.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
                Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
                Assert.Equal(result.Preview.FeeRate, order.CancellationFeeRate);
                Assert.Equal(expectedRefund, order.CancellationRefundAmount);

                var evidence = await CancelEvidenceAsync(db);
                Assert.Equal(
                    JsonNamingPolicy.CamelCase.ConvertName(expectedTier.ToString()),
                    evidence.GetProperty("tier").GetString());
                Assert.Equal(expectedOopsMinutes, evidence.GetProperty("oopsMinutesApplied").GetInt32());
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Membership_That_Lapses_Between_The_Quote_And_The_Cancel_Is_Judged_At_The_Cancel()
    {
        await TestMethod(
            setup: Session(guest: false),
            arrange: Seed(plus: true),
            act: async provider =>
            {
                _clock.Set(_bookedAt.AddMinutes(30));
                var preview = await PreviewAsync(provider, guest: false);

                using (var lapse = provider.CreateScope())
                {
                    var db = lapse.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                    var membership = await db.UserMemberships.IgnoreQueryFilters().SingleAsync();
                    membership.UpdateFromStripeWebhook(
                        "past_due", membership.CurrentPeriodStart, membership.CurrentPeriodEnd, trialEndsAtUtc: null);
                    await db.CommitAsync(CancellationToken.None);
                }

                var charge = await CancelAsync(provider, guest: false);
                return new QuoteAndCharge(preview, charge);
            },
            assert: async (CleansiaDbContext db, QuoteAndCharge result) =>
            {
                Assert.Equal(CancellationFeeTier.FreeOopsWindow, result.Preview.Tier);
                Assert.Equal(0m, result.Preview.FeeAmount);
                Assert.Equal(60, result.Preview.OopsWindowMinutes);

                Assert.Equal(0.50m, result.Charge.FeeRate);
                Assert.Equal(500m, result.Charge.RefundAmount);

                var evidence = await CancelEvidenceAsync(db);
                Assert.Equal("lastMinute", evidence.GetProperty("tier").GetString());
                Assert.Equal(500m, evidence.GetProperty("feeAmount").GetDecimal());
                Assert.Equal(15, evidence.GetProperty("oopsMinutesApplied").GetInt32());
                Assert.Equal(BookingPolicy.FreeCancellationHours, evidence.GetProperty("freeCancellationHoursApplied").GetInt32());
            },
            transactional: false);
    }

    public sealed record QuoteAndCharge(GetCancellationFeePreview.Response Preview, CancelOrder.Response Charge);

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Set(DateTimeOffset at) => now = at;
    }

    private sealed class SucceedingRefunds : IRefundService
    {
        public Task<BusinessResult<RefundResult>> IssueRefundAsync(RefundRequest request, CancellationToken cancellationToken)
            => Task.FromResult(BusinessResult.Success(new RefundResult(
                "refund-oops-window", $"refund:{request.OrderId}:cancel", request.Amount, RefundStatus.Succeeded, false)));
    }
}
