using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// TC-OUTBOX-ATOMIC-0 — the durable backing for the frozen <see cref="IPendingDispatch"/> seam.
/// <see cref="OutboxPendingDispatch.Enqueue{T}"/> writes an <see cref="OutboxMessage"/> row into the
/// same scoped <see cref="CleansiaDbContext"/> the pipeline commits, so the row exists if and only if
/// the business state committed; a double-enqueue of one key collapses onto the table's unique
/// (QueueName, MessageKey); and an early-return without a commit writes nothing. Spins a real
/// DbContext over SQLite in-memory so OnModelCreating + the entity config + the unique index run.
/// </summary>
public sealed class OutboxPendingDispatchTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public OutboxPendingDispatchTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext(string? tenantId)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));
    }

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext(null);
        await ctx.Database.EnsureCreatedAsync();
    }

    private static QueueEnvelope<GenerateReceiptMessage> Envelope(string orderId) =>
        new(MessageKeys.Receipt(orderId), "tenant-1", new GenerateReceiptMessage(orderId, "en"));

    [Fact]
    public async Task Enqueue_With_A_Committed_Business_Change_Persists_Exactly_One_Row()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext(null))
        {
            var dispatch = new OutboxPendingDispatch(ctx);
            dispatch.Enqueue(QueueNames.GenerateReceipt, Envelope("ORDER-1"), MessageKeys.Receipt("ORDER-1"));
            ctx.Languages.Add(Language.Create("xx", "X-Language"));
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var readCtx = NewContext(null);
        var row = Assert.Single(await readCtx.OutboxMessages.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(QueueNames.GenerateReceipt, row.QueueName);
        Assert.Equal(MessageKeys.Receipt("ORDER-1"), row.MessageKey);
        Assert.Equal("tenant-1", row.TenantId);
        Assert.Equal(OutboxMessageStatus.Pending, row.Status);
        Assert.Contains("ORDER-1", row.Body);
        Assert.Single(await readCtx.Languages.Where(l => l.Code == "xx").ToListAsync());
    }

    [Fact]
    public async Task Commit_Throwing_Persists_Neither_The_Row_Nor_The_Business_Change()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext(null))
        {
            var dispatch = new OutboxPendingDispatch(ctx);
            dispatch.Enqueue(QueueNames.GenerateReceipt, Envelope("ORDER-1"), MessageKeys.Receipt("ORDER-1"));
            ctx.Languages.Add(Language.Create("xx", "X-Language"));

            // A second row with the same (QueueName, MessageKey) added straight to the context bypasses
            // the in-request collapse, so the unique index rejects the whole SaveChanges and the unit of
            // work (the outbox row + the business change) rolls back together.
            ctx.OutboxMessages.Add(OutboxMessage.Create(
                QueueNames.GenerateReceipt, MessageKeys.Receipt("ORDER-1"), "{}", "tenant-1"));

            await Assert.ThrowsAnyAsync<Exception>(() => ctx.CommitAsync(CancellationToken.None));
        }

        await using var readCtx = NewContext(null);
        Assert.Empty(await readCtx.OutboxMessages.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await readCtx.Languages.Where(l => l.Code == "xx").ToListAsync());
    }

    [Fact]
    public async Task Two_Enqueues_Same_Key_In_One_Request_Collapse_To_One_Row()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext(null))
        {
            var dispatch = new OutboxPendingDispatch(ctx);
            var envelope = Envelope("ORDER-1");
            dispatch.Enqueue(QueueNames.GenerateReceipt, envelope, MessageKeys.Receipt("ORDER-1"));
            dispatch.Enqueue(QueueNames.GenerateReceipt, envelope, MessageKeys.Receipt("ORDER-1"));
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var readCtx = NewContext(null);
        Assert.Single(await readCtx.OutboxMessages.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Early_Return_Without_A_Commit_Writes_No_Row()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext(null))
        {
            var dispatch = new OutboxPendingDispatch(ctx);
            dispatch.Enqueue(QueueNames.GenerateReceipt, Envelope("ORDER-1"), MessageKeys.Receipt("ORDER-1"));
            // No CommitAsync — the scope is discarded, exactly as the in-memory buffer was.
        }

        await using var readCtx = NewContext(null);
        Assert.Empty(await readCtx.OutboxMessages.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Drain_Returns_Nothing_Because_The_Durable_Row_Is_The_Dispatch_Record()
    {
        await EnsureSchemaAsync();

        await using var ctx = NewContext(null);
        var dispatch = new OutboxPendingDispatch(ctx);
        dispatch.Enqueue(QueueNames.GenerateReceipt, Envelope("ORDER-1"), MessageKeys.Receipt("ORDER-1"));

        Assert.Empty(dispatch.Drain());
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
