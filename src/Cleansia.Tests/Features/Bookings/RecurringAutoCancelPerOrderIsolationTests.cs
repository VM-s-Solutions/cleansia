using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// The hourly auto-cancel retracts each unconfirmed recurring occurrence on its own. One occurrence that fails
/// leaves no trace in another's commit, and holds back none of the others, on this run or the next: it stays as
/// it was, is counted as failed, and is tried again an hour later.
///
/// <para>The failures are injected after the occurrence was already changed in memory, which is the state a
/// shared change tracker carries into the next occurrence's commit. A failure injected before any change would
/// leave nothing behind and prove nothing.</para>
/// </summary>
public sealed class RecurringAutoCancelPerOrderIsolationTests : IDisposable
{
    private const string OrderA = "ord-recurring-a";
    private const string OrderB = "ord-recurring-b";
    private const string OrderC = "ord-recurring-c";
    private const string UserA = "user-recurring-a";
    private const string UserB = "user-recurring-b";
    private const string UserC = "user-recurring-c";

    private readonly SqliteConnection _connection;
    private readonly MutableTenantProvider _tenantProvider = new();

    public RecurringAutoCancelPerOrderIsolationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // The sweep's failure isolation is the subject, not referential integrity.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        using var ctx = NewContext();
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// A's notice throws after A was cancelled in memory. A stays open, with no cancellation and no notice; B is
    /// cancelled with exactly its own notice; and the next run cancels A with its notice.
    /// </summary>
    [Fact]
    public async Task A_Notice_That_Throws_Leaves_Its_Occurrence_Untouched_And_Holds_Back_No_Other()
    {
        await SeedAsync(OrderA, UserA, TestTenants.Default, TimeSpan.FromMinutes(20));
        await SeedAsync(OrderB, UserB, TestTenants.Default, TimeSpan.FromMinutes(40));

        var first = await RunSweepAsync(decorateNotifier: inner => new NoticeThrowsFor(inner, OrderA));

        var a = await ReadOrderAsync(OrderA);
        Assert.Equal(OrderStatus.New, a.CurrentStatus);
        Assert.DoesNotContain(a.OrderStatusHistory, t => t.Status == OrderStatus.Cancelled);
        Assert.Null(a.CancelledAt);
        Assert.Equal(OrderStatus.Cancelled, (await ReadOrderAsync(OrderB)).CurrentStatus);
        Assert.Equal(new[] { PushKey(UserB, OrderB) }, await ReadOutboxKeysAsync());
        Assert.Equal(new[] { UserB }, await ReadFeedRecipientsAsync());
        Assert.Equal(new AutoCancelStaleRecurringOrders.Response(Cancelled: 1, Considered: 2, Failed: 1), first);

        var second = await RunSweepAsync();

        Assert.Equal(OrderStatus.Cancelled, (await ReadOrderAsync(OrderA)).CurrentStatus);
        Assert.Equal(new[] { PushKey(UserA, OrderA), PushKey(UserB, OrderB) }, await ReadOutboxKeysAsync());
        Assert.Equal(new[] { UserA, UserB }, await ReadFeedRecipientsAsync());
        Assert.Equal(new AutoCancelStaleRecurringOrders.Response(Cancelled: 1, Considered: 1, Failed: 0), second);
    }

    /// <summary>
    /// A's row is refused on every save, as a deterministic fault would refuse it. B is still cancelled, and A is
    /// counted as failed on every run while it stays open.
    /// </summary>
    [Fact]
    public async Task An_Occurrence_That_Can_Never_Save_Holds_Back_No_Other()
    {
        await SeedAsync(OrderA, UserA, TestTenants.Default, TimeSpan.FromMinutes(20));
        await SeedAsync(OrderB, UserB, TestTenants.Default, TimeSpan.FromMinutes(40));
        var refuseA = new RefuseEverySaveOf(OrderA);

        var first = await RunSweepAsync(interceptors: [refuseA]);

        Assert.Equal(OrderStatus.Cancelled, (await ReadOrderAsync(OrderB)).CurrentStatus);
        Assert.Equal(OrderStatus.New, (await ReadOrderAsync(OrderA)).CurrentStatus);
        Assert.Equal(new[] { PushKey(UserB, OrderB) }, await ReadOutboxKeysAsync());
        Assert.Equal(new AutoCancelStaleRecurringOrders.Response(Cancelled: 1, Considered: 2, Failed: 1), first);

        var second = await RunSweepAsync(interceptors: [refuseA]);

        Assert.Equal(OrderStatus.New, (await ReadOrderAsync(OrderA)).CurrentStatus);
        Assert.Equal(new AutoCancelStaleRecurringOrders.Response(Cancelled: 0, Considered: 1, Failed: 1), second);
    }

    /// <summary>
    /// Two occurrences of two companies in one run: each cancellation is stamped with its own order's company,
    /// and each notice goes out under its own customer's.
    /// </summary>
    [Fact]
    public async Task Each_Occurrence_Is_Cancelled_Under_Its_Own_Company()
    {
        await SeedAsync(OrderA, UserA, TestTenants.Default, TimeSpan.FromMinutes(20));
        await SeedAsync(OrderB, UserB, TestTenants.Second, TimeSpan.FromMinutes(40));

        var response = await RunSweepAsync();

        Assert.Equal(new AutoCancelStaleRecurringOrders.Response(Cancelled: 2, Considered: 2, Failed: 0), response);
        await using var ctx = NewContext();
        Assert.Equal(
            new[] { $"{OrderA}@{TestTenants.Default}", $"{OrderB}@{TestTenants.Second}" },
            (await ctx.Set<OrderStatusTrack>().IgnoreQueryFilters()
                .Where(t => t.Status == OrderStatus.Cancelled)
                .Select(t => t.OrderId + "@" + t.TenantId)
                .ToListAsync())
            .Order(StringComparer.Ordinal));
        Assert.Equal(
            new[] { $"{UserA}@{TestTenants.Default}", $"{UserB}@{TestTenants.Second}" },
            (await ctx.Set<UserNotification>().IgnoreQueryFilters()
                .Select(n => n.UserId + "@" + n.TenantId)
                .ToListAsync())
            .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The customer cancelled C before paying, so it is still unpaid. The sweep does not take it: it is not
    /// considered, its cancellation keeps who made it, and it gets no second notice.
    /// </summary>
    [Fact]
    public async Task An_Occurrence_Already_Cancelled_While_Unpaid_Is_Not_Considered()
    {
        await SeedAsync(OrderA, UserA, TestTenants.Default, TimeSpan.FromMinutes(20));
        await SeedAsync(OrderC, UserC, TestTenants.Default, TimeSpan.FromMinutes(30), cancelledByCustomer: true);

        var response = await RunSweepAsync();

        var c = await ReadOrderAsync(OrderC);
        Assert.Equal(CancelledBy.Customer, c.CancelledBy);
        Assert.Single(c.OrderStatusHistory, t => t.Status == OrderStatus.Cancelled);
        Assert.Equal(new[] { PushKey(UserA, OrderA) }, await ReadOutboxKeysAsync());
        Assert.Equal(new AutoCancelStaleRecurringOrders.Response(Cancelled: 1, Considered: 1, Failed: 0), response);
    }

    /// <summary>
    /// The unit reads its occurrence again, because the sweep's selection is a snapshot. One paid since, one whose
    /// slot is later than the cutoff it is handed, and one that no longer exists are each left as they are.
    /// </summary>
    [Fact]
    public async Task The_Unit_Leaves_An_Occurrence_That_No_Longer_Awaits_Its_Cutoff()
    {
        await SeedAsync(OrderA, UserA, TestTenants.Default, TimeSpan.FromMinutes(20), paid: true);
        await SeedAsync(OrderB, UserB, TestTenants.Default, TimeSpan.FromHours(3));
        var cutoff = DateTime.UtcNow.AddHours(AutoCancelStaleRecurringOrders.DefaultMissedConfirmGraceHours);

        foreach (var orderId in new[] { OrderA, OrderB, "ord-recurring-gone" })
        {
            var response = await RecurringAutoCancelSweep.SendOccurrenceAsync(
                _connection, new AutoCancelRecurringOccurrence.Command(orderId, cutoff));

            Assert.False(response.Cancelled);
        }

        Assert.Equal(OrderStatus.New, (await ReadOrderAsync(OrderA)).CurrentStatus);
        Assert.Equal(OrderStatus.New, (await ReadOrderAsync(OrderB)).CurrentStatus);
        Assert.Empty(await ReadOutboxKeysAsync());
    }

    private Task<AutoCancelStaleRecurringOrders.Response> RunSweepAsync(
        IInterceptor[]? interceptors = null,
        Func<INotificationProducer, INotificationProducer>? decorateNotifier = null) =>
        RecurringAutoCancelSweep.RunAsync(_connection, interceptors, decorateNotifier);

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            _tenantProvider);

    private async Task SeedAsync(
        string orderId, string userId, string tenantId, TimeSpan cleaningIn, bool cancelledByCustomer = false,
        bool paid = false)
    {
        _tenantProvider.SetTenantOverride(tenantId);
        await using var ctx = NewContext();

        var user = User.CreateWithPassword($"{userId}@cleansia.test", "Password123!", "Rita", "Recurring");
        user.Id = userId;
        user.TenantId = tenantId;
        ctx.Users.Add(user);

        var order = Order.Create(
            customerName: "Rita Recurring",
            customerEmail: $"{userId}@cleansia.test",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.Add(cleaningIn),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: paid ? PaymentStatus.Paid : PaymentStatus.Pending,
            userId: userId,
            recurringTemplateId: $"tmpl-{orderId}",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = orderId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (cancelledByCustomer)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
            order.Cancel(DateTime.UtcNow.AddHours(-1), CancelledBy.Customer, feeRate: 0m, refundAmount: 0m, reason: null);
        }

        ctx.Orders.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
        _tenantProvider.ClearTenantOverride();
    }

    private async Task<Order> ReadOrderAsync(string orderId)
    {
        await using var ctx = NewContext();
        return await ctx.Orders.IgnoreQueryFilters().AsNoTracking()
            .Include(o => o.OrderStatusHistory)
            .SingleAsync(o => o.Id == orderId);
    }

    private async Task<List<string>> ReadOutboxKeysAsync()
    {
        await using var ctx = NewContext();
        return (await ctx.OutboxMessages.IgnoreQueryFilters()
                .Where(m => m.QueueName == QueueNames.NotificationsDispatch)
                .Select(m => m.MessageKey)
                .ToListAsync())
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private async Task<List<string>> ReadFeedRecipientsAsync()
    {
        await using var ctx = NewContext();
        return (await ctx.Set<UserNotification>().IgnoreQueryFilters()
                .Select(n => n.UserId)
                .ToListAsync())
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    private static string PushKey(string userId, string orderId) =>
        MessageKeys.Push(userId, NotificationEventCatalog.OrderCancelled, orderId);

    /// <summary>Throws for the named order's notice, before anything of it is staged.</summary>
    private sealed class NoticeThrowsFor(INotificationProducer inner, string orderId) : INotificationProducer
    {
        public Task NotifyAsync(
            string userId, string eventKey, Dictionary<string, string> args, string? tenantId, string? subject,
            CancellationToken cancellationToken) =>
            subject == orderId
                ? throw new InvalidOperationException($"The notice for {orderId} failed.")
                : inner.NotifyAsync(userId, eventKey, args, tenantId, subject, cancellationToken);

        public Task NotifyEachAsync(
            IReadOnlyCollection<(string UserId, string EventKey, Dictionary<string, string> Args, string? Subject)> notifications,
            CancellationToken cancellationToken) =>
            inner.NotifyEachAsync(notifications, cancellationToken);
    }

    /// <summary>Refuses every save that carries a change to the named order.</summary>
    private sealed class RefuseEverySaveOf(string orderId) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Order>()
                .Any(e => e.Entity.Id == orderId && e.State is EntityState.Modified or EntityState.Added))
            {
                throw new InvalidOperationException($"The save of {orderId} was refused.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class MutableTenantProvider : ITenantProvider
    {
        private string? _tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
