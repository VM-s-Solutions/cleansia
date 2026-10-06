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
using StripeException = Stripe.StripeException;

namespace Cleansia.IntegrationTests.Features.Refunds;

/// <summary>
/// A customer's "service not provided" dispute is resolved with a card refund that Stripe refuses, so the
/// refund waits on the dispute's own key and counts against every other refund on the order. The
/// administrator then confirms the no-show. Against real Postgres with the real refund seam, the dispute
/// stays open and cannot be closed by hand, so resolving it again sends the refund on the same key, and the
/// customer ends with the whole price back.
/// </summary>
[Collection("PostgresCollection")]
public class DisputeRefundPendingAtNoShowTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AdminId = "admin-dispute-no-show";
    private const string CountryId = "country-cz-dispute-no-show";
    private const string CzkId = "cur-czk-dispute-no-show";
    private const string OrderId = "order-dispute-no-show";
    private const string PaymentIntentId = "pi_dispute_no_show";
    private const string NoShowKey = $"refund:{OrderId}:admin";

    private readonly Mock<IStripeClient> _stripe = new();
    private readonly List<(decimal Amount, string Key)> _stripeCalls = [];

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
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

    /// <summary>A 1000 card order whose booked start passed an hour ago, and the customer's dispute that nobody came.</summary>
    private async Task<string> SeedAsync()
    {
        await using var ctx = NewContext();
        ctx.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);
        var currency = Currency.Create("CZK", "CZK", "CZK");
        currency.Id = CzkId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        ctx.Currencies.Add(currency);
        var user = User.CreateWithPassword("dispute-no-show@cleansia.test", "Seed-Password-123", "Dispute", "NoShow");
        ctx.Users.Add(user);
        await ctx.CommitAsync(CancellationToken.None);

        var order = Order.Create(
            customerName: "Dispute NoShow",
            customerEmail: "dispute-no-show@cleansia.test",
            customerPhone: "+420000000005",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(-1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CzkId,
            paymentStatus: PaymentStatus.Paid,
            userId: user.Id,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetMaxEmployees(1);
        order.AssignStripePaymentIntentId(PaymentIntentId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        ctx.Orders.Add(order);
        var dispute = new Dispute(OrderId, user.Id, DisputeReason.ServiceNotProvided, "Nobody came to clean.", user.Id);
        ctx.Disputes.Add(dispute);
        await ctx.CommitAsync(CancellationToken.None);
        return dispute.Id;
    }

    /// <summary>Stripe refuses the first refund it is asked for and takes every one after.</summary>
    private void StripeRefusesTheFirstRefund()
    {
        var refused = false;
        _stripe.Setup(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, decimal, string, CancellationToken>((_, amount, key, _) => _stripeCalls.Add((amount, key)))
            .Returns(() =>
            {
                if (refused)
                {
                    return Task.CompletedTask;
                }

                refused = true;
                return Task.FromException(new StripeException("card network unavailable"));
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
            Mock.Of<IAdminNotifier>(),
            new UserNotificationRepository(ctx),
            NullLogger<RefundService>.Instance);
    }

    private static async Task<T> CommitWhenSucceededAsync<T>(CleansiaDbContext ctx, T result)
        where T : BusinessResult
    {
        if (result.IsSuccess)
        {
            await ctx.CommitAsync(CancellationToken.None);
        }

        return result;
    }

    private async Task<BusinessResult> ResolveAsync(string disputeId, decimal amount)
    {
        await using var ctx = NewContext();
        var handler = new ResolveDispute.Handler(
            new DisputeRepository(ctx),
            new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
            NewRefundService(ctx),
            new RefundRepository(ctx),
            new CreditAccountRepository(ctx),
            new OrderEmployeePayRepository(ctx),
            Mock.Of<ILoyaltyService>(),
            Mock.Of<INotificationProducer>(),
            new AuditContext());
        return await CommitWhenSucceededAsync(
            ctx, await handler.Handle(new ResolveDispute.Command(disputeId, amount, "Nobody came."), CancellationToken.None));
    }

    private async Task<BusinessResult<AdminCancelOrderAsNoShow.Response>> ConfirmNoShowAsync()
    {
        await using var ctx = NewContext();
        var refunds = new RefundRepository(ctx);
        var handler = new AdminCancelOrderAsNoShow.Handler(
            new OrderRepository(ctx),
            new DisputeRepository(ctx),
            refunds,
            new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
            new CleanerNoShowCancellation(
                new CreditAccountRepository(ctx),
                NewRefundService(ctx),
                refunds,
                Mock.Of<INotificationProducer>(),
                new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
                Mock.Of<IPendingDispatch>(),
                NullLogger<CleanerNoShowCancellation>.Instance),
            Mock.Of<INotificationProducer>(),
            Mock.Of<ILiveActivityProducer>(),
            Mock.Of<IExpressWaiverConsumer>(),
            Mock.Of<ILoyaltyService>(),
            TimeProvider.System);
        return await CommitWhenSucceededAsync(
            ctx, await handler.Handle(new AdminCancelOrderAsNoShow.Command(OrderId), CancellationToken.None));
    }

    private async Task<BusinessResult<UpdateDisputeStatus.Response>> CloseAsync(string disputeId)
    {
        await using var ctx = NewContext();
        var handler = new UpdateDisputeStatus.Handler(
            new DisputeRepository(ctx),
            new RefundRepository(ctx),
            new TestUserSessionProvider(AdminId, "admin@cleansia.test"));
        return await CommitWhenSucceededAsync(
            ctx, await handler.Handle(new UpdateDisputeStatus.Command(disputeId, DisputeStatus.Closed), CancellationToken.None));
    }

    private async Task<DisputeStatus> DisputeStatusAsync(string disputeId)
    {
        await using var ctx = NewContext();
        return await ctx.Disputes.IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.Id == disputeId)
            .Select(d => d.Status)
            .SingleAsync();
    }

    /// <summary>
    /// The dispute's 1000 is refused, so the no-show finds the whole card already owed and refunds nothing;
    /// a dispute's 400 is refused, so the no-show refunds the 600 the card has left. Either way the dispute is
    /// still open afterwards, and resolving it again sends Stripe the same amount on the dispute's key.
    /// </summary>
    [Theory]
    [InlineData(1000, null)]
    [InlineData(400, 600)]
    public async Task A_Dispute_Refund_Stripe_Refused_Is_Still_Paid_After_The_No_Show(int disputeRefund, int? noShowRefund)
    {
        await ResetAsync();
        var disputeId = await SeedAsync();
        var disputeKey = $"refund:{OrderId}:dispute:{disputeId}";
        StripeRefusesTheFirstRefund();

        var refused = await ResolveAsync(disputeId, disputeRefund);
        var noShow = await ConfirmNoShowAsync();
        var statusAfterNoShow = await DisputeStatusAsync(disputeId);
        var closing = await CloseAsync(disputeId);
        var resolved = await ResolveAsync(disputeId, disputeRefund);

        Assert.Equal(BusinessErrorMessage.RefundFailed, refused.Error?.Message);
        Assert.True(noShow.IsSuccess, noShow.Error?.Message);
        Assert.Equal(noShowRefund, (int?)noShow.Value!.RefundedAmount);
        Assert.Equal(DisputeStatus.Pending, statusAfterNoShow);
        Assert.Equal(BusinessErrorMessage.DisputeRefundPending, closing.Error?.Message);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal(DisputeStatus.Resolved, await DisputeStatusAsync(disputeId));
        var expectedCalls = new List<(decimal, string)> { (disputeRefund, disputeKey) };
        if (noShowRefund is { } refundedAtNoShow)
        {
            expectedCalls.Add((refundedAtNoShow, NoShowKey));
        }

        expectedCalls.Add((disputeRefund, disputeKey));
        Assert.Equal(expectedCalls, _stripeCalls);

        await using var ctx = NewContext();
        var refundedByCard = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.OrderId == OrderId && r.Status == RefundStatus.Succeeded)
            .SumAsync(r => r.Amount);
        var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
        Assert.Equal(1000m, refundedByCard);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
    }

    /// <summary>
    /// The no-show's refund and the complaint's card refund of the whole 1000 were claimed at the same moment and
    /// both refused. Confirming the no-show pays the 1000 on the older key, its own, and leaves the complaint open
    /// with its row pending, so it cannot be closed by hand. Stripe never refunds more than a charge, so it cannot
    /// have paid the complaint's refund: resolving the complaint again closes that row as not paid and resolves it
    /// with nothing sent. The customer has the 1000 back, once.
    /// </summary>
    [Fact]
    public async Task A_Dispute_Refund_Confirmed_Refunds_Left_No_Card_For_Is_Closed_And_The_Dispute_Resolved()
    {
        await ResetAsync();
        var disputeId = await SeedAsync();
        var disputeKey = $"refund:{OrderId}:dispute:{disputeId}";
        var claimedOn = DateTimeOffset.UtcNow.AddHours(-2);
        await using (var ctx = NewContext())
        {
            var noShow = Refund.Create(OrderId, NoShowKey, 1000m, "CZK", RefundReason.ServiceNotRendered, RefundSource.AppRefund);
            noShow.Created(AdminId, claimedOn);
            var complaint = Refund.Create(OrderId, disputeKey, 1000m, "CZK", RefundReason.DisputeResolution,
                RefundSource.AppRefund, disputeId: disputeId);
            complaint.Created(AdminId, claimedOn.AddMilliseconds(3));
            ctx.Refunds.AddRange(noShow, complaint);
            await ctx.CommitAsync(CancellationToken.None);
        }

        _stripe.Setup(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, decimal, string, CancellationToken>((_, amount, key, _) => _stripeCalls.Add((amount, key)))
            .Returns(Task.CompletedTask);

        var noShowConfirmed = await ConfirmNoShowAsync();
        var closing = await CloseAsync(disputeId);
        var resolved = await ResolveAsync(disputeId, 1000m);

        Assert.True(noShowConfirmed.IsSuccess, noShowConfirmed.Error?.Message);
        Assert.Equal(1000m, noShowConfirmed.Value!.RefundedAmount);
        Assert.Equal(BusinessErrorMessage.DisputeRefundPending, closing.Error?.Message);
        Assert.True(resolved.IsSuccess, resolved.Error?.Message);
        Assert.Equal(DisputeStatus.Resolved, await DisputeStatusAsync(disputeId));
        Assert.Equal([(1000m, NoShowKey)], _stripeCalls);

        await using var read = NewContext();
        var complaintRow = await read.Refunds.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.RefundKey == disputeKey);
        Assert.Equal(RefundStatus.Failed, complaintRow.Status);
        var refundedByCard = await read.Refunds.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.OrderId == OrderId && r.Status == RefundStatus.Succeeded)
            .SumAsync(r => r.Amount);
        var givenBackInCredit = await read.CreditTransactions.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.OrderId == OrderId
                && (t.Reason == CreditTransactionReason.OrderPaymentReturned
                    || t.Reason == CreditTransactionReason.DisputeSettlement))
            .SumAsync(t => t.Amount);
        Assert.Equal(1000m, refundedByCard);
        Assert.Equal(1000m, refundedByCard + givenBackInCredit);
        var order = await read.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
