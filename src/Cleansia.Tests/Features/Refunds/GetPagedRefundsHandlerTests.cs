using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Features.Refunds.DTOs;
using Cleansia.Core.AppServices.Features.Refunds.Filters;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// The administrator's refunds list, run over a real context on SQLite so the state filter, the tenant
/// filter and the paging translate: six states read from status, failed date and repaid date; the origin
/// read from the key; and the retry offered only where a retry can send money.
/// </summary>
public sealed class GetPagedRefundsHandlerTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Currency _czk = Currency.Create("CZK", "Kč", "Czech koruna");

    public GetPagedRefundsHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    public async Task InitializeAsync()
    {
        await using var ctx = NewContext();
        await TestTenants.EnsureCreatedWithRegistryAsync(ctx);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private CleansiaDbContext NewContext(string tenant = TestTenants.Default) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenant));

    private Order NewOrder(string id, string? userId = "user-1", OrderStatus status = OrderStatus.New)
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana@cleansia.test",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Main St 1", "Praha", "14000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: _czk.Id,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking,
            userId: userId);
        order.Id = id;
        order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        return order;
    }

    private static Refund NewRefund(
        Order order, RefundReason reason = RefundReason.AdminDiscretion, string? requestId = "lines-1",
        string? disputeId = null, DateTimeOffset? createdOn = null)
    {
        var refund = Refund.Create(
            order.Id,
            RefundService.BuildRefundKey(new RefundRequest(order.Id, 0m, reason, "admin", disputeId, requestId)),
            300m,
            "CZK",
            reason,
            RefundSource.AppRefund,
            disputeId: disputeId);
        refund.Created("seed", createdOn ?? DateTimeOffset.UtcNow);
        return refund;
    }

    private async Task SeedAsync(IEnumerable<Order> orders, IEnumerable<Refund> refunds, string tenant = TestTenants.Default)
    {
        await using var ctx = NewContext(tenant);
        ctx.AddRange(orders.Cast<object>());
        ctx.AddRange(refunds.Cast<object>());
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task StampRepaidAsync(string refundId)
    {
        await using var ctx = NewContext();
        var refund = await ctx.Set<Refund>().SingleAsync(r => r.Id == refundId);
        ctx.Entry(refund).Property(r => r.RepaidOn).CurrentValue = DateTimeOffset.UtcNow;
        ctx.Entry(refund).Property(r => r.RepaymentMethod).CurrentValue = RefundRepaymentMethod.BankTransfer;
        ctx.Entry(refund).Property(r => r.RepaymentReference).CurrentValue = "VS-42";
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<PagedData<RefundListItem>> HandleAsync(RefundFilter? filter = null)
    {
        await using var ctx = NewContext();
        var handlerType = typeof(GetPagedRefunds).GetNestedType(
            "Handler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!;
        var handler = Activator.CreateInstance(handlerType, new RefundRepository(ctx))!;
        return await (Task<PagedData<RefundListItem>>)handlerType.GetMethod("Handle")!
            .Invoke(handler, [new GetPagedRefunds.Request { Filter = filter }, CancellationToken.None])!;
    }

    private static RefundFilter States(params RefundState[] states) => new(states, null, null, null, null);

    /// <summary>One refund in each of the six states, on one order.</summary>
    private async Task<Dictionary<RefundState, string>> SeedSixStatesAsync()
    {
        var order = NewOrder("order-six");
        var notSent = NewRefund(order, requestId: "a");
        var refunded = NewRefund(order, requestId: "b").MarkSucceeded("re_b", DateTimeOffset.UtcNow);
        var bounced = NewRefund(order, requestId: "c").MarkSucceeded("re_c", DateTimeOffset.UtcNow);
        bounced.MarkFailedByStripe("re_c", DateTimeOffset.UtcNow);
        var failed = NewRefund(order, requestId: "d");
        failed.MarkFailedByStripe("re_d", DateTimeOffset.UtcNow);
        var repaid = NewRefund(order, requestId: "e");
        repaid.MarkFailedByStripe("re_e", DateTimeOffset.UtcNow);
        var closed = NewRefund(order, requestId: "f").MarkFailed();
        await SeedAsync([order], [notSent, refunded, bounced, failed, repaid, closed]);
        await StampRepaidAsync(repaid.Id);
        return new()
        {
            [RefundState.NotSent] = notSent.Id,
            [RefundState.Refunded] = refunded.Id,
            [RefundState.Bounced] = bounced.Id,
            [RefundState.Failed] = failed.Id,
            [RefundState.Repaid] = repaid.Id,
            [RefundState.Closed] = closed.Id,
        };
    }

    [Fact]
    public async Task Each_Of_The_Six_States_Maps_From_Status_Failed_Date_And_Repaid_Date()
    {
        var ids = await SeedSixStatesAsync();

        var page = await HandleAsync();

        Assert.Equal(6, page.Total);
        foreach (var (state, id) in ids)
        {
            Assert.Equal(state.ToString(), page.Data.Single(r => r.Id == id).State.Name);
        }

        var repaid = page.Data.Single(r => r.Id == ids[RefundState.Repaid]);
        Assert.NotNull(repaid.RepaidOn);
        Assert.Equal(nameof(RefundRepaymentMethod.BankTransfer), repaid.RepaymentMethod!.Name);
        Assert.Equal("VS-42", repaid.RepaymentReference);
        Assert.Equal("re_d", page.Data.Single(r => r.Id == ids[RefundState.Failed]).StripeRefundId);
    }

    [Fact]
    public async Task Filtering_By_One_State_Returns_Exactly_The_Rows_Mapped_To_It()
    {
        var ids = await SeedSixStatesAsync();

        foreach (var (state, id) in ids)
        {
            var page = await HandleAsync(States(state));
            Assert.Equal(id, Assert.Single(page.Data).Id);
        }
    }

    [Fact]
    public async Task Needs_Action_Returns_Not_Sent_Failed_And_Bounced_Only_And_A_Closed_Refund_Never_Reads_As_Owed()
    {
        var ids = await SeedSixStatesAsync();

        var page = await HandleAsync(States(RefundState.NotSent, RefundState.Failed, RefundState.Bounced));

        Assert.Equal(
            new HashSet<string> { ids[RefundState.NotSent], ids[RefundState.Failed], ids[RefundState.Bounced] },
            page.Data.Select(r => r.Id).ToHashSet());
    }

    [Fact]
    public async Task Another_Companys_Refunds_Never_Appear()
    {
        var own = NewOrder("order-own");
        var foreign = NewOrder("order-foreign");
        await SeedAsync([own], [NewRefund(own)]);
        await SeedAsync([foreign], [NewRefund(foreign)], TestTenants.Second);

        var page = await HandleAsync();

        Assert.Equal("order-own", Assert.Single(page.Data).OrderId);
    }

    [Fact]
    public async Task Newest_First_And_Filtered_By_Customer_Order_And_Requested_Date()
    {
        var t0 = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        var a = NewOrder("order-a", userId: "user-a");
        var b = NewOrder("order-b", userId: "user-b");
        var older = NewRefund(a, requestId: "old", createdOn: t0);
        var newer = NewRefund(a, requestId: "new", createdOn: t0.AddDays(2));
        var other = NewRefund(b, createdOn: t0.AddDays(1));
        await SeedAsync([a, b], [older, newer, other]);

        Assert.Equal([newer.Id, other.Id, older.Id], (await HandleAsync()).Data.Select(r => r.Id));
        Assert.Equal([newer.Id, older.Id], (await HandleAsync(new(null, "user-a", null, null, null))).Data.Select(r => r.Id));
        Assert.Equal([other.Id], (await HandleAsync(new(null, null, "order-b", null, null))).Data.Select(r => r.Id));
        Assert.Equal([other.Id], (await HandleAsync(new(null, null, null, t0.AddHours(1), t0.AddDays(1).AddHours(1)))).Data.Select(r => r.Id));
    }

    [Fact]
    public async Task Origin_Reads_From_The_Key_And_A_Disputes_Row_Carries_Its_Dispute()
    {
        var order = NewOrder("order-origin", status: OrderStatus.Cancelled);
        var cancel = NewRefund(order, RefundReason.CustomerCancellation, requestId: null);
        var noShow = NewRefund(order, RefundReason.ServiceNotRendered, requestId: null);
        var dispute = NewRefund(order, RefundReason.DisputeResolution, requestId: null, disputeId: "dispute-1");
        var full = NewRefund(order, RefundReason.AdminDiscretion, requestId: "full");
        var partial = NewRefund(order, RefundReason.ServiceNotRendered, requestId: "lines-9");
        await SeedAsync([order], [cancel, noShow, dispute, full, partial]);

        var rows = (await HandleAsync()).Data.ToDictionary(r => r.Id);

        Assert.Equal(nameof(RefundOrigin.Cancellation), rows[cancel.Id].Origin.Name);
        Assert.Equal(nameof(RefundOrigin.Cancellation), rows[noShow.Id].Origin.Name);
        Assert.Equal(nameof(RefundOrigin.Dispute), rows[dispute.Id].Origin.Name);
        Assert.Equal("dispute-1", rows[dispute.Id].DisputeId);
        Assert.Equal(nameof(RefundOrigin.FullRefund), rows[full.Id].Origin.Name);
        Assert.Equal(nameof(RefundOrigin.PartialRefund), rows[partial.Id].Origin.Name);
        Assert.Null(rows[full.Id].DisputeId);
    }

    [Fact]
    public async Task Can_Retry_Only_A_Not_Sent_Refund_That_Is_Not_A_Disputes_And_A_Cancellations_Only_On_A_Cancelled_Order()
    {
        var cancelled = NewOrder("order-cancelled", status: OrderStatus.Cancelled);
        var open = NewOrder("order-open", userId: null);
        var cancelOnCancelled = NewRefund(cancelled, RefundReason.CustomerCancellation, requestId: null);
        var partialNotSent = NewRefund(cancelled, requestId: "p1");
        var fullNotSent = NewRefund(cancelled, requestId: "full");
        var disputeNotSent = NewRefund(cancelled, RefundReason.DisputeResolution, requestId: null, disputeId: "d-1");
        var partialFailed = NewRefund(cancelled, requestId: "p2");
        partialFailed.MarkFailedByStripe("re_x", DateTimeOffset.UtcNow);
        var partialRefunded = NewRefund(cancelled, requestId: "p3").MarkSucceeded("re_y", DateTimeOffset.UtcNow);
        var partialClosed = NewRefund(cancelled, requestId: "p4").MarkFailed();
        var guestCancelOnOpen = NewRefund(open, RefundReason.CustomerCancellation, requestId: null);
        await SeedAsync(
            [cancelled, open],
            [cancelOnCancelled, partialNotSent, fullNotSent, disputeNotSent, partialFailed, partialRefunded, partialClosed, guestCancelOnOpen]);

        var rows = (await HandleAsync()).Data.ToDictionary(r => r.Id);

        Assert.True(rows[cancelOnCancelled.Id].CanRetry);
        Assert.True(rows[partialNotSent.Id].CanRetry);
        Assert.True(rows[fullNotSent.Id].CanRetry);
        Assert.False(rows[disputeNotSent.Id].CanRetry);
        Assert.False(rows[partialFailed.Id].CanRetry);
        Assert.False(rows[partialRefunded.Id].CanRetry);
        Assert.False(rows[partialClosed.Id].CanRetry);
        Assert.False(rows[guestCancelOnOpen.Id].CanRetry);
        Assert.False(rows[guestCancelOnOpen.Id].HasAccount);
        Assert.Null(rows[guestCancelOnOpen.Id].UserId);
        Assert.True(rows[cancelOnCancelled.Id].HasAccount);
        Assert.Equal("Jana Nováková", rows[cancelOnCancelled.Id].CustomerName);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
