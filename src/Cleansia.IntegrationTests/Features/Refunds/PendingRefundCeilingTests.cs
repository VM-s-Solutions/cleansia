using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Refunds;

/// <summary>
/// A card refund Stripe paid but whose answer never came back stays pending on its key. Against real Postgres
/// with the real refund seam, every other refund's ceiling and every complaint settled in credit counts it as
/// money already on its way back, and its own retry sends Stripe the same amount on the same key, so the order
/// never gives back more than it took.
/// </summary>
[Collection("PostgresCollection")]
public class PendingRefundCeilingTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-pending-ceiling";
    private const string CzkId = "cur-czk-pending-ceiling";
    private const string OrderId = "order-pending-ceiling";
    private const string PaymentIntentId = "pi_pending_ceiling";
    private const string CancelKey = $"refund:{OrderId}:cancel";

    private readonly Mock<IStripeClient> _stripe = new();
    private readonly List<(decimal Amount, string Key)> _stripeCalls = [];

    private CleansiaDbContext NewContext(string tenantId = TestTenants.Default)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("admin-pending-ceiling", "admin@cleansia.test"),
            new FixedTenantProvider(tenantId));
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

    /// <summary>A paid card order, <paramref name="creditApplied"/> of it paid in credit, and a complaint on it that chose credit.</summary>
    private async Task<(string UserId, string DisputeId)> SeedAsync(
        decimal total, decimal creditApplied, OrderStatus status = OrderStatus.Completed)
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
            var user = User.CreateWithPassword("pending-ceiling@cleansia.test", "Seed-Password-123", "Pending", "Ceiling");
            ctx.Users.Add(user);
            await ctx.CommitAsync(CancellationToken.None);
            userId = user.Id;
        }

        await using (var ctx = NewContext())
        {
            var account = await new CreditAccountRepository(ctx).EnsureForUserAsync(userId, CzkId, CancellationToken.None);
            if (creditApplied > 0m)
            {
                account!.Issue(creditApplied, CreditTransactionReason.Goodwill, "seed-grant", "seed", note: "n");
            }

            var order = Order.Create(
                customerName: "Pending Ceiling",
                customerEmail: "pending-ceiling@cleansia.test",
                customerPhone: "+420000000004",
                customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
                rooms: 1,
                bathrooms: 1,
                cleaningDateTime: status == OrderStatus.Completed ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(10),
                paymentType: PaymentType.Card,
                totalPrice: total,
                currencyId: CzkId,
                paymentStatus: PaymentStatus.Paid,
                userId: userId,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = OrderId;
            order.SetMaxEmployees(1);
            order.ApplyCredit(creditApplied, userId);
            order.AssignStripePaymentIntentId(PaymentIntentId);
            order.AddOrderStatus(OrderStatusTrack.Create(status, order));
            ctx.Orders.Add(order);
            var dispute = new Dispute(
                OrderId, userId, DisputeReason.QualityIssue, "The bathroom was not cleaned.", userId,
                DisputeSettlementPreference.Credit);
            ctx.Disputes.Add(dispute);
            await ctx.CommitAsync(CancellationToken.None);

            if (creditApplied > 0m)
            {
                Assert.True(await new CreditAccountRepository(ctx).TryDebitAsync(
                    account!.Id, creditApplied, CreditTransactionReason.OrderPayment, $"order-payment-{OrderId}", userId,
                    CancellationToken.None, orderId: OrderId));
            }

            return (userId, dispute.Id);
        }
    }

    /// <summary>Stripe takes every refund; the first call on <paramref name="timesOutOn"/> is taken and then times out.</summary>
    private void StripePays(string? timesOutOn = null)
    {
        var timedOut = false;
        _stripe.Setup(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, decimal, string, CancellationToken>((_, amount, key, _) => _stripeCalls.Add((amount, key)))
            .Returns((string _, decimal _, string key, CancellationToken _) =>
            {
                if (key != timesOutOn || timedOut)
                {
                    return Task.CompletedTask;
                }

                timedOut = true;
                return Task.FromException(new HttpRequestException("timed out"));
            });
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

    private static RefundRequest Partial(decimal amount) =>
        new(OrderId, amount, RefundReason.AdminDiscretion, "admin-pending-ceiling", RefundRequestId: "partial-1");

    private async Task<BusinessResult<RefundResult>> RefundAsync(RefundRequest request)
    {
        await using var ctx = NewContext();
        return await NewRefundService(ctx).IssueRefundAsync(request, CancellationToken.None);
    }

    private async Task<BusinessResult> SettleInCreditAsync(string disputeId, decimal amount)
    {
        await using var ctx = NewContext();
        var handler = new ResolveDispute.Handler(
            new DisputeRepository(ctx),
            new TestUserSessionProvider("admin-pending-ceiling", "admin@cleansia.test"),
            NewRefundService(ctx),
            new RefundRepository(ctx),
            new CreditAccountRepository(ctx),
            new OrderEmployeePayRepository(ctx),
            Mock.Of<ILoyaltyService>(),
            Mock.Of<INotificationProducer>(),
            new AuditContext());
        var result = await handler.Handle(new ResolveDispute.Command(disputeId, amount, "Settled in credit."), CancellationToken.None);
        if (result.IsSuccess)
        {
            await ctx.CommitAsync(CancellationToken.None);
        }

        return result;
    }

    private sealed record GivenBack(decimal Card, decimal CreditReturned, decimal SettledInCredit, PaymentStatus PaymentStatus)
    {
        public decimal Total => Card + CreditReturned + SettledInCredit;
    }

    private async Task<GivenBack> ReadAsync()
    {
        await using var ctx = NewContext();
        var card = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.OrderId == OrderId && r.Status == RefundStatus.Succeeded)
            .SumAsync(r => r.Amount);
        var returned = await ctx.CreditTransactions.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.OrderId == OrderId && t.Reason == CreditTransactionReason.OrderPaymentReturned)
            .SumAsync(t => t.Amount);
        var settled = await ctx.CreditTransactions.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.OrderId == OrderId && t.Reason == CreditTransactionReason.DisputeSettlement)
            .SumAsync(t => t.Amount);
        var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
        return new GivenBack(card, returned, settled, order.PaymentStatus);
    }

    [Fact]
    public async Task The_Pending_Total_Sums_Pending_Rows_Only_Leaves_Out_The_Named_Key_And_Reads_Its_Own_Company()
    {
        await ResetAsync();
        await SeedAsync(total: 1000m, creditApplied: 0m);
        await using (var ctx = NewContext())
        {
            Refund Row(string purpose, decimal amount) =>
                Refund.Create(OrderId, $"refund:{OrderId}:admin:{purpose}", amount, "CZK", RefundReason.AdminDiscretion,
                    RefundSource.AppRefund);
            ctx.Refunds.AddRange(
                Row("a", 100m),
                Row("b", 200m),
                Row("c", 400m).MarkSucceeded(stripeRefundId: null, confirmedOnUtc: DateTimeOffset.UtcNow),
                Row("d", 50m).MarkFailed());
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            var refunds = new RefundRepository(ctx);
            Assert.Equal(300m, await refunds.GetPendingRefundTotalForOrderAsync(OrderId, null, CancellationToken.None));
            Assert.Equal(200m, await refunds.GetPendingRefundTotalForOrderAsync(
                OrderId, $"refund:{OrderId}:admin:a", CancellationToken.None));
            Assert.Equal(300m, await refunds.GetPendingRefundTotalForOrderAsync(
                OrderId, $"refund:{OrderId}:admin:c", CancellationToken.None));
        }

        await using (var other = NewContext(TestTenants.Second))
        {
            Assert.Equal(0m, await new RefundRepository(other).GetPendingRefundTotalForOrderAsync(
                OrderId, null, CancellationToken.None));
        }
    }

    /// <summary>
    /// A retry's card ceiling counts the pending refunds claimed before it on its order: not itself, even when
    /// the instance in hand carries its creation time to a finer tick than Postgres stored, not a later claim,
    /// and not one that went through or was closed.
    /// </summary>
    [Fact]
    public async Task The_Pending_Total_Claimed_Before_A_Refund_Counts_Only_Older_Pending_Rows()
    {
        await ResetAsync();
        await SeedAsync(total: 1000m, creditApplied: 0m);
        var claimedOn = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        Refund Row(string purpose, decimal amount, TimeSpan after)
        {
            var refund = Refund.Create(OrderId, $"refund:{OrderId}:admin:{purpose}", amount, "CZK",
                RefundReason.AdminDiscretion, RefundSource.AppRefund);
            refund.Created("seed", claimedOn + after);
            return refund;
        }

        await using var ctx = NewContext();
        var self = Row("self", 300m, TimeSpan.FromTicks(7));
        ctx.Refunds.AddRange(
            Row("older-a", 100m, TimeSpan.FromSeconds(-2)),
            Row("older-b", 200m, TimeSpan.FromSeconds(-1)),
            Row("older-paid", 400m, TimeSpan.FromSeconds(-3)).MarkSucceeded(stripeRefundId: null, confirmedOnUtc: claimedOn),
            Row("older-closed", 50m, TimeSpan.FromSeconds(-4)).MarkFailed(),
            self,
            Row("younger", 500m, TimeSpan.FromSeconds(1)));
        await ctx.CommitAsync(CancellationToken.None);

        var refunds = new RefundRepository(ctx);
        Assert.Equal(300m, await refunds.GetPendingRefundTotalClaimedBeforeAsync(self, CancellationToken.None));
        var younger = await refunds.GetByRefundKeyAsync($"refund:{OrderId}:admin:younger", CancellationToken.None);
        Assert.Equal(600m, await refunds.GetPendingRefundTotalClaimedBeforeAsync(younger!, CancellationToken.None));

        await using var other = NewContext(TestTenants.Second);
        Assert.Equal(0m, await new RefundRepository(other).GetPendingRefundTotalClaimedBeforeAsync(younger!, CancellationToken.None));
    }

    /// <summary>
    /// 1000 by card. A member's cancellation and, a moment later, an administrator's full refund were claimed at
    /// once, and Stripe paid neither. The re-drive of the older one is not held back by the younger, which
    /// counted it: Stripe pays the 1000 on the cancellation's key, and the full refund's retry sends nothing.
    /// </summary>
    [Fact]
    public async Task Of_Two_Refunds_Claimed_At_Once_And_Paid_By_Neither_The_Older_Ones_Redrive_Pays_It()
    {
        await ResetAsync();
        await SeedAsync(total: 1000m, creditApplied: 0m, status: OrderStatus.Cancelled);
        var full = new RefundRequest(OrderId, 1000m, RefundReason.AdminDiscretion, "admin-pending-ceiling", RefundRequestId: "full");
        var claimedOn = DateTimeOffset.UtcNow.AddHours(-2);
        string cancelId;
        await using (var ctx = NewContext())
        {
            var cancel = Refund.Create(OrderId, CancelKey, 1000m, "CZK", RefundReason.CustomerCancellation, RefundSource.AppRefund);
            cancel.Created("member", claimedOn);
            var admin = Refund.Create(OrderId, RefundService.BuildRefundKey(full), 1000m, "CZK", RefundReason.AdminDiscretion,
                RefundSource.AppRefund);
            admin.Created("admin", claimedOn.AddMilliseconds(3));
            ctx.Refunds.AddRange(cancel, admin);
            await ctx.CommitAsync(CancellationToken.None);
            cancelId = cancel.Id;
        }

        StripePays();
        BusinessResult<RefundResult> redriven;
        await using (var ctx = NewContext())
        {
            redriven = await NewRefundService(ctx).RedriveAsync(cancelId, "system", CancellationToken.None);
        }

        var retried = await RefundAsync(full);

        Assert.True(redriven.IsSuccess, redriven.Error?.Message);
        Assert.Equal([(1000m, CancelKey)], _stripeCalls);
        Assert.Equal(BusinessErrorMessage.RefundNothingRefundable, retried.Error?.Message);
        var given = await ReadAsync();
        Assert.Equal(1000m, given.Card);
        Assert.Equal(PaymentStatus.Refunded, given.PaymentStatus);
    }

    /// <summary>
    /// 1000 by card. An admin's partial refund of 600 is paid by Stripe, whose answer never comes back, so the
    /// row stays pending and the order still reads paid. The customer chose credit for a complaint: 1000 is
    /// refused, the 400 not already on its way back is settled, and the partial's retry is answered by Stripe
    /// with the refund it already made. 600 + 400 is the price.
    /// </summary>
    [Fact]
    public async Task A_Credit_Settlement_Is_Held_To_What_A_Pending_Card_Refund_Leaves()
    {
        await ResetAsync();
        var (_, disputeId) = await SeedAsync(total: 1000m, creditApplied: 0m);
        var partialKey = RefundService.BuildRefundKey(Partial(600m));
        StripePays(timesOutOn: partialKey);

        await Assert.ThrowsAsync<HttpRequestException>(() => RefundAsync(Partial(600m)));
        var refused = await SettleInCreditAsync(disputeId, 1000m);
        var settled = await SettleInCreditAsync(disputeId, 400m);
        var retried = await RefundAsync(Partial(600m));

        Assert.Equal(BusinessErrorMessage.InvalidRefundAmount, refused.Error?.Message);
        Assert.True(settled.IsSuccess, settled.Error?.Message);
        Assert.True(retried.IsSuccess, retried.Error?.Message);
        Assert.Equal([(600m, partialKey), (600m, partialKey)], _stripeCalls);
        var given = await ReadAsync();
        Assert.Equal(600m, given.Card);
        Assert.Equal(400m, given.SettledInCredit);
        Assert.Equal(1000m, given.Total);
    }

    /// <summary>
    /// 1000 by card. An admin's partial refund of 300 is paid by Stripe and left pending. A free cancellation
    /// asks for the whole price and the card is sent the 700 still on the charge, which real Stripe would pay,
    /// not 1000, which it would refuse every hour. The partial's retry then records the 300 it already made.
    /// </summary>
    [Fact]
    public async Task A_Free_Cancellation_Sends_The_Card_Only_What_A_Pending_Refund_Leaves()
    {
        await ResetAsync();
        var (userId, _) = await SeedAsync(total: 1000m, creditApplied: 0m, status: OrderStatus.New);
        var partialKey = RefundService.BuildRefundKey(Partial(300m));
        StripePays(timesOutOn: partialKey);

        await Assert.ThrowsAsync<HttpRequestException>(() => RefundAsync(Partial(300m)));
        await using (var ctx = NewContext())
        {
            var cancellation = new CustomerOrderCancellation(
                new FixedTenantProvider(TestTenants.Default),
                NewRefundService(ctx),
                new RefundRepository(ctx),
                new ReceivableRepository(ctx),
                new CreditAccountRepository(ctx),
                Mock.Of<ILoyaltyService>(),
                new CancellationPolicyResolver(new UserMembershipRepository(ctx), new OrderRepository(ctx)),
                Mock.Of<INotificationProducer>(),
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
            var cancelled = await cancellation.ExecuteAsync(order, reason: null, userId, CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
            Assert.True(cancelled.Response.RefundInitiated);
        }

        var retried = await RefundAsync(Partial(300m));

        Assert.True(retried.IsSuccess, retried.Error?.Message);
        Assert.Equal([(300m, partialKey), (700m, CancelKey), (300m, partialKey)], _stripeCalls);
        var given = await ReadAsync();
        Assert.Equal(1000m, given.Card);
        Assert.Equal(PaymentStatus.Refunded, given.PaymentStatus);
    }

    /// <summary>
    /// 2000, 500 in credit and 1500 by card. An admin's partial refund of 800 freezes 600 on the card; Stripe
    /// pays it, the answer never comes back, and its 200 credit share is never returned. A complaint settled in
    /// credit is held to the 1400 the sale has left. The retry sends Stripe the same 600 on the same key, which
    /// Stripe answers with the refund it made, rather than a smaller card share it would refuse every time, and
    /// returns no credit: 600 + 1400 is the price.
    /// </summary>
    [Fact]
    public async Task A_Retried_Refund_Sends_Stripe_The_Same_Amount_After_A_Settlement_Took_Its_Credit_Share()
    {
        await ResetAsync();
        var (_, disputeId) = await SeedAsync(total: 2000m, creditApplied: 500m);
        var partialKey = RefundService.BuildRefundKey(Partial(800m));
        StripePays(timesOutOn: partialKey);

        await Assert.ThrowsAsync<HttpRequestException>(() => RefundAsync(Partial(800m)));
        var refused = await SettleInCreditAsync(disputeId, 1400.01m);
        var settled = await SettleInCreditAsync(disputeId, 1400m);
        var retried = await RefundAsync(Partial(800m));

        Assert.Equal(BusinessErrorMessage.InvalidRefundAmount, refused.Error?.Message);
        Assert.True(settled.IsSuccess, settled.Error?.Message);
        Assert.True(retried.IsSuccess, retried.Error?.Message);
        Assert.Equal([(600m, partialKey), (600m, partialKey)], _stripeCalls);
        var given = await ReadAsync();
        Assert.Equal(600m, given.Card);
        Assert.Equal(0m, given.CreditReturned);
        Assert.Equal(1400m, given.SettledInCredit);
        Assert.Equal(2000m, given.Total);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
