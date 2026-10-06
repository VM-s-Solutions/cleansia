using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;
using StripeException = Stripe.StripeException;
using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// A signed-in customer cancels a 2000 card order that took 500 of credit, while Stripe is down or
/// refusing — against real Postgres with the real refund seam. The cancel completes, the card refund
/// waits Pending on its key, the 500 of credit comes back at once on that same key, and the hourly
/// re-drive then refunds the card exactly once without returning the credit a second time.
/// </summary>
[Collection("PostgresCollection")]
public class MemberCancellationRefundRedriveTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-member-redrive";
    private const string CzkId = "cur-czk-member-redrive";
    private const string OrderId = "order-member-redrive";
    private const string PaymentIntentId = "pi_member_redrive";
    private const string RefundKey = $"refund:{OrderId}:cancel";
    private const decimal Total = 2000m;
    private const decimal Credit = 500m;
    private const decimal CardShare = 1500m;

    private readonly Mock<IStripeClient> _stripe = new();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));
    }

    private async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        });
        await respawner.ResetAsync(conn);
        await SeedTenantRegistryAsync(conn);
    }

    private async Task<string> SeedAsync(decimal total = Total, decimal credit = Credit, decimal settledInCredit = 0m)
    {
        string userId;
        await using (var ctx = NewContext())
        {
            ctx.Languages.Add(Language.Create("en", "English"));
            var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
            country.Id = CountryId;
            ctx.Countries.Add(country);
            var currency = Currency.Create("CZK", "CZK", "CZK");
            currency.Id = CzkId;
            currency.IsActive = true;
            currency.SetAsDefault(true);
            ctx.Currencies.Add(currency);
            var user = User.CreateWithPassword("member-redrive@cleansia.test", "Seed-Password-123", "Member", "Redrive");
            ctx.Users.Add(user);
            await ctx.CommitAsync(CancellationToken.None);
            userId = user.Id;
        }

        await using (var ctx = NewContext())
        {
            var account = await new CreditAccountRepository(ctx).EnsureForUserAsync(userId, CzkId, CancellationToken.None);
            if (credit > 0m)
            {
                account!.Issue(credit, CreditTransactionReason.Goodwill, "seed-grant", "seed", note: "n");
            }

            if (settledInCredit > 0m)
            {
                account!.Issue(
                    settledInCredit, CreditTransactionReason.DisputeSettlement, "dispute-settlement:dispute-earlier",
                    "admin", orderId: OrderId, disputeId: "dispute-earlier");
            }

            var order = Order.Create(
                customerName: "Member Redrive",
                customerEmail: "member-redrive@cleansia.test",
                customerPhone: "+420000000001",
                customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
                rooms: 1,
                bathrooms: 1,
                cleaningDateTime: DateTime.UtcNow.AddDays(10),
                paymentType: PaymentType.Card,
                totalPrice: total,
                currencyId: CzkId,
                paymentStatus: PaymentStatus.Paid,
                userId: userId,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = OrderId;
            order.SetMaxEmployees(1);
            order.ApplyCredit(credit, userId);
            order.AssignStripePaymentIntentId(PaymentIntentId);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
            ctx.Orders.Add(order);
            await ctx.CommitAsync(CancellationToken.None);

            if (credit > 0m)
            {
                Assert.True(await new CreditAccountRepository(ctx).TryDebitAsync(
                    account!.Id, credit, CreditTransactionReason.OrderPayment, $"order-payment-{OrderId}", userId,
                    CancellationToken.None, orderId: OrderId));
            }
        }

        return userId;
    }

    private RefundService NewRefundService(CleansiaDbContext ctx)
    {
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        return new RefundService(
            new RefundRepository(ctx),
            new OrderRepository(ctx),
            new CreditAccountRepository(ctx),
            factory.Object,
            NullLogger<RefundService>.Instance);
    }

    private NotificationProducer NewProducer(CleansiaDbContext ctx) =>
        new(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx), new UserRepository(ctx),
            NullLogger<NotificationProducer>.Instance);

    private async Task<CancelOrder.Response> CancelAsync(string userId)
    {
        await using var ctx = NewContext();
        var cancellation = new CustomerOrderCancellation(
            new FixedTenantProvider(TestTenants.Default),
            NewRefundService(ctx),
            new RefundRepository(ctx),
            new ReceivableRepository(ctx),
            new CreditAccountRepository(ctx),
            Mock.Of<ILoyaltyService>(),
            new CancellationPolicyResolver(new UserMembershipRepository(ctx), new OrderRepository(ctx)),
            NewProducer(ctx),
            Mock.Of<ILiveActivityProducer>(),
            Mock.Of<IExpressWaiverConsumer>(),
            Mock.Of<IPendingDispatch>(),
            new AuditContext(),
            TimeProvider.System,
            NullLogger<CustomerOrderCancellation>.Instance);
        var order = await ctx.Orders
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.AssignedEmployees)
            .Include(o => o.Currency)
            .SingleAsync(o => o.Id == OrderId);

        var result = await cancellation.ExecuteAsync(order, reason: null, userId, CancellationToken.None);
        await ctx.CommitAsync(CancellationToken.None);
        return result.Response;
    }

    private async Task<RedrivePendingRefunds.Response> RedriveAsync(int hoursOld = 2, IAdminNotifier? adminNotifier = null)
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE \"Refunds\" SET \"CreatedOn\" = NOW() - {0} * INTERVAL '1 hour'", hoursOld);
        }

        await using var watchdogContext = NewContext();
        var watchdog = new RedrivePendingRefunds.Handler(
            new RefundRepository(watchdogContext),
            NewRefundService(watchdogContext),
            NewProducer(watchdogContext),
            adminNotifier ?? Mock.Of<IAdminNotifier>(),
            new UserNotificationRepository(watchdogContext),
            new FixedTenantProvider(TestTenants.Default),
            watchdogContext,
            NullLogger<RedrivePendingRefunds.Handler>.Instance);
        var result = await watchdog.Handle(new RedrivePendingRefunds.Command(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    public static TheoryData<Exception> StripeFailures() =>
    [
        new HttpRequestException("connection reset"),
        new StripeException("card network unavailable"),
    ];

    [Theory]
    [MemberData(nameof(StripeFailures))]
    public async Task The_Cancel_Completes_The_Credit_Comes_Back_Now_And_The_Card_Once_On_The_Redrive(Exception failure)
    {
        await ResetAsync();
        var userId = await SeedAsync();
        _stripe.SetupSequence(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), RefundKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure)
            .Returns(Task.CompletedTask);

        var response = await CancelAsync(userId);

        Assert.False(response.RefundInitiated);
        Assert.True(response.RefundPending);
        await using (var ctx = NewContext())
        {
            var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
            Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
            var refund = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking().SingleAsync();
            Assert.Equal(RefundStatus.Pending, refund.Status);
            Assert.Equal(CardShare, refund.Amount);
            var returned = await ctx.CreditTransactions.AsNoTracking()
                .SingleAsync(t => t.OrderId == OrderId && t.Reason == CreditTransactionReason.OrderPaymentReturned);
            Assert.Equal(Credit, returned.Amount);
            Assert.Equal($"credit-return:{RefundKey}", returned.IdempotencyKey);
        }

        var redrive = await RedriveAsync();

        Assert.Equal(1, redrive.Redriven);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, CardShare, RefundKey, It.IsAny<CancellationToken>()), Times.Exactly(2));
        await using (var ctx = NewContext())
        {
            var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
            Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
            Assert.Equal(RefundStatus.Succeeded, (await ctx.Refunds.IgnoreQueryFilters().AsNoTracking().SingleAsync()).Status);
            var account = await ctx.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
                .Include(a => a.Transactions)
                .SingleAsync(a => a.UserId == userId && a.CurrencyId == CzkId);
            Assert.Single(account.Transactions, t => t.Reason == CreditTransactionReason.OrderPaymentReturned);
            Assert.Equal(Credit, account.Balance);
            Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        }
    }

    /// <summary>
    /// The same cancellation of a 1000 sale paid 700 by card and 300 in credit, after a complaint on it was
    /// settled in 200 of credit. 800 is left: the seam freezes the card's 560 and the credit's 240 comes back
    /// at once on the refund's key. Whether the first call failed at Stripe or timed out after Stripe took it,
    /// the re-drive sends the same 560 on the same key, and the customer ends with exactly the 1000 paid.
    /// </summary>
    [Theory]
    [MemberData(nameof(StripeFailures))]
    public async Task After_A_Complaint_Settled_In_Credit_The_Redrive_Repeats_The_Same_Card_Amount_And_The_Customer_Gets_The_Price(
        Exception failure)
    {
        await ResetAsync();
        var userId = await SeedAsync(total: 1000m, credit: 300m, settledInCredit: 200m);
        var stripeCalls = new List<decimal>();
        _stripe.Setup(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), RefundKey, It.IsAny<CancellationToken>()))
            .Callback<string, decimal, string, CancellationToken>((_, amount, _, _) => stripeCalls.Add(amount))
            .Returns(() => stripeCalls.Count == 1 ? Task.FromException(failure) : Task.CompletedTask);

        var response = await CancelAsync(userId);

        Assert.True(response.RefundPending);
        await using (var ctx = NewContext())
        {
            var refund = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking().SingleAsync();
            Assert.Equal(RefundStatus.Pending, refund.Status);
            Assert.Equal(560m, refund.Amount);
            var returned = await ctx.CreditTransactions.AsNoTracking()
                .SingleAsync(t => t.OrderId == OrderId && t.Reason == CreditTransactionReason.OrderPaymentReturned);
            Assert.Equal(240m, returned.Amount);
            Assert.Equal($"credit-return:{RefundKey}", returned.IdempotencyKey);
        }

        var redrive = await RedriveAsync();

        Assert.Equal(1, redrive.Redriven);
        Assert.Equal([560m, 560m], stripeCalls);
        await using (var ctx = NewContext())
        {
            var refund = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking().SingleAsync();
            Assert.Equal(RefundStatus.Succeeded, refund.Status);
            Assert.Equal(560m, refund.Amount);
            var account = await ctx.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
                .Include(a => a.Transactions)
                .SingleAsync(a => a.UserId == userId && a.CurrencyId == CzkId);
            var creditReturned = account.Transactions
                .Where(t => t.Reason == CreditTransactionReason.OrderPaymentReturned).Sum(t => t.Amount);
            var settled = account.Transactions
                .Where(t => t.Reason == CreditTransactionReason.DisputeSettlement).Sum(t => t.Amount);
            Assert.Equal(240m, creditReturned);
            Assert.Equal(1000m, refund.Amount + creditReturned + settled);
            Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        }
    }

    private async Task<string> CancelWhileStripeTimesOutAsync()
    {
        await ResetAsync();
        var userId = await SeedAsync();
        _stripe.Setup(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), RefundKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timed out"));
        Assert.True((await CancelAsync(userId)).RefundPending);
        return userId;
    }

    private void StripeLists(bool failed) =>
        _stripe.Setup(s => s.FindRefundAsync(
                It.IsAny<string?>(), PaymentIntentId, RefundKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StripeRefundSnapshot("re_1", CardShare, failed));

    /// <summary>
    /// Stripe took the card refund but its answer never came back, and a day later Stripe has forgotten the key.
    /// The re-drive finds the refund Stripe lists under the key and records it, so the card is never paid twice.
    /// </summary>
    [Fact]
    public async Task A_Redrive_Records_The_Refund_Stripe_Lists_Under_Its_Key_And_Sends_Nothing()
    {
        var userId = await CancelWhileStripeTimesOutAsync();
        StripeLists(failed: false);

        var redrive = await RedriveAsync(hoursOld: 25);

        Assert.Equal(1, redrive.Redriven);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        await using var ctx = NewContext();
        var refund = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal("re_1", refund.StripeRefundId);
        Assert.Equal(CardShare, refund.Amount);
        var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        var account = await ctx.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
            .Include(a => a.Transactions)
            .SingleAsync(a => a.UserId == userId && a.CurrencyId == CzkId);
        Assert.Single(account.Transactions, t => t.Reason == CreditTransactionReason.OrderPaymentReturned);
    }

    /// <summary>
    /// Stripe made the card refund and then failed it. The re-drive sends nothing, closes the row, and the
    /// administrators are asked to retry it at once, though the row is two hours old.
    /// </summary>
    [Fact]
    public async Task A_Redrive_That_Finds_Stripe_Failed_The_Refund_Closes_It_And_Raises_It_At_Once()
    {
        await CancelWhileStripeTimesOutAsync();
        StripeLists(failed: true);
        var admins = new Mock<IAdminNotifier>();

        var redrive = await RedriveAsync(hoursOld: 2, admins.Object);

        Assert.Equal(0, redrive.Redriven);
        Assert.Equal(1, redrive.Alerted);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        await using var ctx = NewContext();
        var refund = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal(RefundStatus.Failed, refund.Status);
        admins.Verify(n => n.NotifyAsync(
            It.Is<AdminEvent>(e => e.Key == AdminNotificationEventCatalog.RefundNeedsRetry && e.Subject == refund.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        admins.VerifyNoOtherCalls();
    }

    private async Task PartlyRefundAsync(decimal amount)
    {
        await using var ctx = NewContext();
        var partial = await NewRefundService(ctx).IssueRefundAsync(
            new RefundRequest(OrderId, amount, RefundReason.AdminDiscretion, "admin", RefundRequestId: "partial-1"),
            CancellationToken.None);
        Assert.True(partial.IsSuccess, partial.Error?.Message);
        var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
        Assert.Equal(PaymentStatus.PartiallyRefunded, order.PaymentStatus);
    }

    /// <summary>
    /// 1000 by card, of which an admin already refunded 300. A free cancellation sends the card the 700 still
    /// on the charge, which ends the order refunded, and records 700 as what the cancellation gave back.
    /// </summary>
    [Fact]
    public async Task A_Free_Cancel_Of_A_Partly_Refunded_Card_Order_Refunds_The_Rest_Of_The_Card()
    {
        await ResetAsync();
        var userId = await SeedAsync(total: 1000m, credit: 0m);
        await PartlyRefundAsync(300m);

        var response = await CancelAsync(userId);

        Assert.True(response.RefundInitiated);
        Assert.Equal(700m, response.RefundAmount);
        Assert.Equal(700m, response.ActualRefundAmount);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, 700m, RefundKey, It.IsAny<CancellationToken>()), Times.Once);
        await using var ctx = NewContext();
        var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        Assert.Equal(700m, order.CancellationRefundAmount);
        Assert.Equal(1000m, await ctx.Refunds.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.Status == RefundStatus.Succeeded).SumAsync(r => r.Amount));
    }

    /// <summary>
    /// 1000 paid 700 by card and 300 in credit, with a complaint settled in credit and an admin's partial refund
    /// split across both tenders. A free cancellation gives back what the sale has left in the same mix, so the
    /// customer ends with exactly the 1000 paid: after a partial of 400 and 200 settled, 280 card and 120 credit;
    /// after a partial of 800 and 150 settled, 35 card and 15 credit.
    /// </summary>
    [Theory]
    [InlineData(200, 400, 280, 120)]
    [InlineData(150, 800, 35, 15)]
    public async Task A_Free_Cancel_Of_A_Partly_Refunded_Card_And_Credit_Order_Gives_Back_The_Rest_Of_The_Sale(
        int settledInCredit, int partial, int card, int credit)
    {
        await ResetAsync();
        var userId = await SeedAsync(total: 1000m, credit: 300m, settledInCredit: settledInCredit);
        await PartlyRefundAsync(partial);

        var response = await CancelAsync(userId);

        Assert.True(response.RefundInitiated);
        Assert.Equal(card + credit, response.RefundAmount);
        Assert.Equal(card, response.ActualRefundAmount);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, card, RefundKey, It.IsAny<CancellationToken>()), Times.Once);
        await using var ctx = NewContext();
        var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
        Assert.Equal(card + credit, order.CancellationRefundAmount);
        var cardRefunded = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.Status == RefundStatus.Succeeded).SumAsync(r => r.Amount);
        var account = await ctx.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
            .Include(a => a.Transactions)
            .SingleAsync(a => a.UserId == userId && a.CurrencyId == CzkId);
        var returnedOnTheCancellation = account.Transactions
            .Where(t => t.IdempotencyKey == $"credit-return:{RefundKey}").Sum(t => t.Amount);
        var creditReturned = account.Transactions
            .Where(t => t.Reason == CreditTransactionReason.OrderPaymentReturned).Sum(t => t.Amount);
        Assert.Equal(credit, returnedOnTheCancellation);
        Assert.Equal(1000m, cardRefunded + creditReturned + settledInCredit);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
