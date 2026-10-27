using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// A guest's booking cancelled by the platform — nobody took it, the payment never finished, or an admin (or
/// a company closing) called it off — reaches the guest's inbox end to end: the sweep or the cancellation
/// stages the e-mail with the booking, every link the guest held is retired, and the send carries one fresh
/// link and only the refund that went through. Before, the e-mail consumer took only a guest's own
/// cancellation and these bookings were never mentioned to their guest at all.
/// </summary>
[Collection("PostgresCollection")]
public class GuestPlatformCancellationEmailTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "guest-platform-cancel";
    private const string Email = "guest-platform@example.test";
    private const string CurrencyId = "guest-platform-czk";
    private const string CountryId = "guest-platform-cz";
    private const string PaymentIntentId = "pi_guest_platform";

    private string _oldToken = null!;

    private sealed class Run
    {
        public readonly Mock<IStripeClient> Stripe = new();
        public readonly Mock<IEmailService> EmailService = new();
        public string? DeliveredTo;
        public decimal? DeliveredAmount;
        public string? DeliveredToken;
        public decimal? DeliveredApology;
        public int Deliveries;

        public Task Setup(IServiceCollection services)
        {
            var factory = new Mock<IStripeClientFactory>();
            factory.Setup(x => x.CreateClient()).Returns(Stripe.Object);
            services.Replace(ServiceDescriptor.Singleton(factory.Object));
            EmailService.Setup(x => x.SendOrderStatusUpdateEmailAsync(It.IsAny<string>(), It.IsAny<Order>(),
                    "Cancelled", It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<decimal?>(), It.IsAny<string?>(),
                    It.IsAny<decimal?>(), It.IsAny<bool>()))
                .Returns((string to, Order _, string _, string _, CancellationToken _, decimal? amount, string? token,
                    decimal? apology, bool _) =>
                {
                    DeliveredTo = to;
                    DeliveredAmount = amount;
                    DeliveredToken = token;
                    DeliveredApology = apology;
                    Deliveries++;
                    return Task.FromResult("sent");
                });
            services.Replace(ServiceDescriptor.Scoped<IEmailService>(_ => EmailService.Object));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_Guest_Booking_Nobody_Took_Is_Emailed_With_A_Fresh_Link_And_The_Refund()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup,
            arrange: db => Seed(db, PaymentStatus.Paid, cleaningDateTime: DateTime.UtcNow.AddHours(-2)),
            act: async provider =>
            {
                using (var sweep = provider.CreateScope())
                {
                    var result = await ActivatorUtilities.CreateInstance<CancelUnfilledOrders.Handler>(sweep.ServiceProvider)
                        .Handle(new CancelUnfilledOrders.Command(), CancellationToken.None);
                    Assert.Equal(1, result.Value!.CancelledCount);
                }
                await DeliverAsync(provider);
                return true;
            },
            assert: async (CleansiaDbContext db, bool _) =>
            {
                await AssertDeliveredWithFreshLinkAsync(db, run);
                Assert.Equal(1000m, run.DeliveredAmount);
            }, transactional: false);
    }

    /// <summary>
    /// A customer with an account who uses only the web has no push and no inbox: the booking's own address
    /// gets the cancellation e-mail too, with the refund and the apology credit, and no guest credential is
    /// minted for it — the button opens the order page they sign in to.
    /// </summary>
    [Fact]
    public async Task An_Account_Booking_Nobody_Took_Is_Emailed_With_The_Refund_And_The_Apology()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup,
            arrange: db => Seed(db, PaymentStatus.Paid, cleaningDateTime: DateTime.UtcNow.AddHours(-2), withAccount: true),
            act: async provider =>
            {
                using (var sweep = provider.CreateScope())
                {
                    var result = await ActivatorUtilities.CreateInstance<CancelUnfilledOrders.Handler>(sweep.ServiceProvider)
                        .Handle(new CancelUnfilledOrders.Command(), CancellationToken.None);
                    Assert.Equal(1, result.Value!.CancelledCount);
                    Assert.Equal(1, result.Value.CreditedCount);
                }
                await DeliverAsync(provider);
                return true;
            },
            assert: async (CleansiaDbContext db, bool _) =>
            {
                Assert.Equal(1, run.Deliveries);
                Assert.Equal(Email, run.DeliveredTo);
                Assert.Null(run.DeliveredToken);
                Assert.Equal(1000m, run.DeliveredAmount);
                Assert.Equal(250m, run.DeliveredApology);
                Assert.Empty(await db.GuestOrderAccessTokens.IgnoreQueryFilters().Where(t => t.OrderId == OrderId).ToListAsync());
            }, transactional: false);
    }

    [Fact]
    public async Task A_Guest_Checkout_That_Was_Never_Paid_Is_Emailed_With_No_Refund_Claimed()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup,
            arrange: db => Seed(db, PaymentStatus.Pending, cleaningDateTime: DateTime.UtcNow.AddDays(2)),
            act: async provider =>
            {
                using (var sweep = provider.CreateScope())
                {
                    var result = await ActivatorUtilities.CreateInstance<CleanupStalePendingOrders.Handler>(sweep.ServiceProvider)
                        .Handle(new CleanupStalePendingOrders.Command(), CancellationToken.None);
                    Assert.Equal(1, result.Value!.CancelledCount);
                }
                await DeliverAsync(provider);
                return true;
            },
            assert: async (CleansiaDbContext db, bool _) =>
            {
                await AssertDeliveredWithFreshLinkAsync(db, run);
                Assert.Null(run.DeliveredAmount);
            }, transactional: false);
    }

    [Fact]
    public async Task A_Guest_Booking_An_Admin_Cancelled_Whose_Refund_Stripe_Refused_Is_Not_Told_It_Was_Refunded()
    {
        var run = new Run();
        await TestMethod<bool>(
            setup: services =>
            {
                run.Setup(services);
                run.Stripe.Setup(x => x.RefundPaymentIntentAsync(PaymentIntentId, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Stripe.StripeException("recording Stripe refusal"));
                return Task.CompletedTask;
            },
            arrange: db => Seed(db, PaymentStatus.Paid, cleaningDateTime: DateTime.UtcNow.AddDays(2)),
            act: async provider =>
            {
                using (var cancel = provider.CreateScope())
                {
                    var db = cancel.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                    var order = await db.Orders
                        .Include(o => o.OrderStatusHistory)
                        .Include(o => o.AssignedEmployees).ThenInclude(a => a.Employee)
                        .SingleAsync(o => o.Id == OrderId);
                    var result = await cancel.ServiceProvider.GetRequiredService<IPlatformOrderCancellation>().CancelAsync(
                        order, "admin-guest-platform", CancelledBy.Admin, "Double booking on our side",
                        RefundReason.CustomerCancellation, CancellationToken.None);
                    Assert.False(result.Refund.Initiated);
                    await db.CommitAsync(CancellationToken.None);
                }
                await DeliverAsync(provider);
                return true;
            },
            assert: async (CleansiaDbContext db, bool _) =>
            {
                await AssertDeliveredWithFreshLinkAsync(db, run);
                Assert.Null(run.DeliveredAmount);
            }, transactional: false);
    }

    private static async Task DeliverAsync(IServiceProvider provider)
    {
        string body;
        using (var read = provider.CreateScope())
        {
            body = await read.ServiceProvider.GetRequiredService<CleansiaDbContext>().OutboxMessages.IgnoreQueryFilters()
                .Where(x => x.QueueName == QueueNames.SendEmail && x.MessageKey == MessageKeys.GuestOrderCancelledEmail(OrderId))
                .Select(x => x.Body)
                .SingleAsync();
        }

        using var send = provider.CreateScope();
        await ActivatorUtilities.CreateInstance<SendEmailHandler>(send.ServiceProvider).HandleAsync(body, CancellationToken.None);
    }

    private async Task AssertDeliveredWithFreshLinkAsync(CleansiaDbContext db, Run run)
    {
        Assert.Equal(1, run.Deliveries);
        Assert.Equal(Email, run.DeliveredTo);
        Assert.False(string.IsNullOrEmpty(run.DeliveredToken));
        Assert.NotEqual(_oldToken, run.DeliveredToken);

        var tokens = await db.GuestOrderAccessTokens.IgnoreQueryFilters().Where(t => t.OrderId == OrderId).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, t => t.RevokedOn is null);
    }

    private async Task Seed(
        CleansiaDbContext db, PaymentStatus paymentStatus, DateTime cleaningDateTime, bool withAccount = false)
    {
        db.Languages.Add(Language.Create("en", "English"));
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        currency.SetNoShowCredit(250m);
        db.Currencies.Add(currency);
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        db.Countries.Add(country);

        User? account = null;
        if (withAccount)
        {
            account = User.CreateWithPassword("account-platform@example.test", "Seed-Password-123", "Jana", "Nováková");
            db.Users.Add(account);
        }

        var order = Order.Create("Guest", Email, "+420777000111",
            Address.Create("Guest Street", "Praha", "11000", CountryId), 2, 1,
            cleaningDateTime, PaymentType.Card, 1000m, CurrencyId, paymentStatus, userId: account?.Id,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetMaxEmployees(1);
        order.AssignStripePaymentIntentId(PaymentIntentId);
        order.Created("seed", DateTimeOffset.UtcNow.AddHours(-3));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (paymentStatus == PaymentStatus.Paid)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        }
        db.Orders.Add(order);

        if (account is null)
        {
            var token = GuestOrderAccessToken.Issue(order.Id, GuestOrderAccessToken.ExpiryFor(order.CleaningDateTime));
            _oldToken = token.RawToken!;
            db.GuestOrderAccessTokens.Add(token);
        }

        await db.CommitAsync(CancellationToken.None);
    }
}
