using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// The assignment push keys, against the real unique index — the only thing that can speak here.
///
/// <para><b>Why this class exists.</b> <see cref="OutboxPendingDispatch"/> collapses a repeated key
/// through a <c>_seen</c> HashSet, and <see cref="OutboxPendingDispatchTests"/> proves that works. But
/// <c>_seen</c> is a field on a SCOPED instance: it dedupes within ONE request and knows nothing about
/// any other. Across two requests the same key reaches <c>INSERT</c> twice and
/// <c>IX_OutboxMessages_QueueName_MessageKey</c> raises 23505 — which surfaces at the pipeline's commit,
/// after the handler has returned, so no handler-level <c>catch</c> can see it. The business change
/// rolls back with it.</para>
///
/// <para>Retention does not save it either: dispatched rows are kept 14 days
/// (<c>OutboxRetentionConfig.DispatchedRetentionDays</c>), and a row that dead-letters to
/// <c>Failed</c> is never pruned at all, so a poisoned key stays poisoned.</para>
///
/// <para><b>The subject segment is therefore the whole defence</b>, and it must identify the EVENT, not
/// the order. Every case below is a state the platform reaches on an ordinary day.</para>
/// </summary>
public sealed class AssignmentPushKeyCollisionTests : IDisposable
{
    private const string CustomerUserId = "user-customer";
    private const string OrderId = "order-1";

    private readonly SqliteConnection _connection;

    public AssignmentPushKeyCollisionTests()
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
            new FixedTenantProvider(null));
    }

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    /// <summary>One request: enqueue a push under <paramref name="subject"/> and commit, as the pipeline does.</summary>
    private async Task EnqueueInItsOwnRequestAsync(string eventKey, string recipientUserId, string subject)
    {
        await using var ctx = NewContext();
        var dispatch = new OutboxPendingDispatch(ctx);
        var key = MessageKeys.Push(recipientUserId, eventKey, subject);
        dispatch.Enqueue(
            QueueNames.NotificationsDispatch,
            new QueueEnvelope<SendPushNotificationMessage>(
                key, null, new SendPushNotificationMessage(recipientUserId, eventKey, [], null)),
            key);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<int> RowCountAsync()
    {
        await using var ctx = NewContext();
        return await ctx.OutboxMessages.IgnoreQueryFilters().CountAsync();
    }

    /// <summary>
    /// <b>The ordinary path, and the reason this is not an admin-only defect.</b> A crew is
    /// <c>ceil(EstimatedTime / 120)</c>, so every 180- or 240-minute service in the seeded catalogue
    /// carries two cleaners. Both takes tell the SAME customer about the SAME order, so a subject of
    /// <c>order.Id</c> alone makes the second cleaner's take collide — and its rollback takes the seat
    /// with it, so the job can never be fully crewed.
    /// </summary>
    [Fact]
    public async Task Two_Cleaners_Taking_One_Two_Seat_Job_Do_Not_Collide()
    {
        await EnsureSchemaAsync();

        await EnqueueInItsOwnRequestAsync(
            NotificationEventCatalog.OrderCleanerAssigned, CustomerUserId, Subject("assignment-a"));
        await EnqueueInItsOwnRequestAsync(
            NotificationEventCatalog.OrderCleanerAssigned, CustomerUserId, Subject("assignment-b"));

        Assert.Equal(2, await RowCountAsync());
    }

    /// <summary>
    /// The admin reassign: the customer was told when the first cleaner took it, and is told again when
    /// a different cleaner takes over. Two distinct assignment rows, so two distinct subjects.
    /// </summary>
    [Fact]
    public async Task A_Take_Then_An_Admin_Reassign_Do_Not_Collide()
    {
        await EnsureSchemaAsync();

        await EnqueueInItsOwnRequestAsync(
            NotificationEventCatalog.OrderCleanerAssigned, CustomerUserId, Subject("assignment-taken"));
        await EnqueueInItsOwnRequestAsync(
            NotificationEventCatalog.OrderCleanerAssigned, CustomerUserId, Subject("assignment-reassigned"));

        Assert.Equal(2, await RowCountAsync());
    }

    /// <summary>
    /// An admin undoing their own reassign — A, then B, then back to A. The EMPLOYEE id repeats here, so
    /// only a per-assignment discriminator survives this. <c>UnassignEmployee</c> hard-deletes the row
    /// and <c>AddAssignedEmployee</c> creates a new one with a fresh ULID, which is exactly why the
    /// assignment row's own id is the right subject and the employee's is not.
    /// </summary>
    [Fact]
    public async Task Reassigning_Back_To_The_First_Cleaner_Does_Not_Collide()
    {
        await EnsureSchemaAsync();

        foreach (var assignmentId in new[] { "assignment-a1", "assignment-b1", "assignment-a2" })
        {
            await EnqueueInItsOwnRequestAsync(
                NotificationEventCatalog.OrderAssigned, "user-cleaner-a", Subject(assignmentId));
        }

        Assert.Equal(3, await RowCountAsync());
    }

    /// <summary>
    /// The guarantee the subject must NOT give up. Two enqueues of the genuinely same event still
    /// collapse — that is what the unique index is for, and a discriminator that made every enqueue
    /// unique would have thrown the dedup away to fix the collision.
    /// </summary>
    [Fact]
    public async Task The_Same_Assignment_Enqueued_Twice_Still_Collapses_To_One_Row()
    {
        await EnsureSchemaAsync();

        await using var ctx = NewContext();
        var dispatch = new OutboxPendingDispatch(ctx);
        var key = MessageKeys.Push(
            CustomerUserId, NotificationEventCatalog.OrderCleanerAssigned, Subject("assignment-a"));

        for (var i = 0; i < 2; i++)
        {
            dispatch.Enqueue(
                QueueNames.NotificationsDispatch,
                new QueueEnvelope<SendPushNotificationMessage>(
                    key, null, new SendPushNotificationMessage(CustomerUserId, "k", [], null)),
                key);
        }

        await ctx.CommitAsync(CancellationToken.None);

        Assert.Equal(1, await RowCountAsync());
    }

    /// <summary>
    /// The characterization case: this is what the shipped subject was, and it is why the three cases
    /// above were 500s. Kept so that anyone tempted to "simplify" the subject back to the order id sees
    /// the failure mode spelled out rather than rediscovering it in production.
    /// </summary>
    [Fact]
    public async Task An_Order_Id_Subject_Is_Exactly_What_Collides()
    {
        await EnsureSchemaAsync();

        await EnqueueInItsOwnRequestAsync(
            NotificationEventCatalog.OrderCleanerAssigned, CustomerUserId, OrderId);

        var second = await Record.ExceptionAsync(() => EnqueueInItsOwnRequestAsync(
            NotificationEventCatalog.OrderCleanerAssigned, CustomerUserId, OrderId));

        Assert.NotNull(second);
        Assert.IsType<DbUpdateException>(second);
        Assert.Equal(1, await RowCountAsync());
    }

    private static string Subject(string assignmentId) =>
        AssignmentNotificationSubject.For(OrderId, assignmentId);

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
