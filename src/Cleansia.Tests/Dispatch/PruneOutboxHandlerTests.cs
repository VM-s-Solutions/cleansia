using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Messaging;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// The retention-prune predicate against a real <see cref="CleansiaDbContext"/>: only terminal
/// <see cref="OutboxMessageStatus.Dispatched"/> rows past the window are deleted; a Pending or Failed row
/// and a recently-dispatched row are untouched; old <see cref="ProcessedMessage"/> idempotency rows are
/// pruned but recent ones survive; and an <c>AdminActionAudit</c> row is NEVER touched (append-only). The
/// prune is read-terminal-then-delete only, so dispatch and idempotency invariants are unchanged.
/// </summary>
public sealed class PruneOutboxHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PruneOutboxHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(null));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    private static OutboxMessage DispatchedRow(string key, DateTimeOffset dispatchedOn)
    {
        var row = OutboxMessage.Create(QueueNames.GenerateReceipt, key, "{}", null);
        row.MarkDispatched(dispatchedOn);
        return row;
    }

    private static OutboxMessage PendingRow(string key) =>
        OutboxMessage.Create(QueueNames.GenerateReceipt, key, "{}", null);

    private static OutboxMessage FailedRow(string key)
    {
        var row = OutboxMessage.Create(QueueNames.GenerateReceipt, key, "{}", null);
        row.MarkFailed("permanently broken");
        return row;
    }

    private async Task<PruneOutbox.Response> RunAsync(PruneOutbox.Command command)
    {
        await using var ctx = NewContext();
        var handler = new PruneOutbox.Handler(
            new OutboxMessageRepository(ctx),
            new ProcessedMessageRepository(ctx),
            ctx,
            NewConfig(),
            NullLogger<PruneOutbox.Handler>.Instance);

        var result = await handler.Handle(command, CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static IOutboxRetentionConfig NewConfig() => new StubConfig
    {
        Enabled = true,
        DispatchedRetentionDays = 14,
        ProcessedRetentionDays = 14,
        BatchSize = 500,
    };

    [Fact]
    public async Task Only_Dispatched_Rows_Past_The_Window_Are_Deleted()
    {
        await EnsureSchemaAsync();
        var oldDispatched = DispatchedRow("receipt:OLD", DateTimeOffset.UtcNow.AddDays(-30));
        var recentDispatched = DispatchedRow("receipt:RECENT", DateTimeOffset.UtcNow.AddDays(-1));
        var pending = PendingRow("receipt:PENDING");
        var failed = FailedRow("receipt:FAILED");

        await using (var ctx = NewContext())
        {
            ctx.OutboxMessages.AddRange(oldDispatched, recentDispatched, pending, failed);
            await ctx.CommitAsync(CancellationToken.None);
        }

        var response = await RunAsync(new PruneOutbox.Command());

        Assert.Equal(1, response.PrunedOutboxCount);

        await using var verify = NewContext();
        var survivors = await verify.OutboxMessages.IgnoreQueryFilters()
            .Select(m => m.MessageKey).OrderBy(k => k).ToListAsync();
        Assert.Equal(new[] { "receipt:FAILED", "receipt:PENDING", "receipt:RECENT" }, survivors);
    }

    [Fact]
    public async Task A_Pending_Or_Failed_Row_Is_Never_Pruned_However_Old()
    {
        await EnsureSchemaAsync();
        var ancientPending = PendingRow("receipt:ANCIENT-PENDING");
        var ancientFailed = FailedRow("receipt:ANCIENT-FAILED");

        await using (var ctx = NewContext())
        {
            ctx.OutboxMessages.AddRange(ancientPending, ancientFailed);
            await ctx.CommitAsync(CancellationToken.None);
            // Age them well past the window via their CreatedOn so any cutoff-on-the-wrong-column bug surfaces.
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE \"OutboxMessages\" SET \"CreatedOn\" = {0}, \"DispatchedOn\" = {0}",
                DateTimeOffset.UtcNow.AddDays(-365));
        }

        var response = await RunAsync(new PruneOutbox.Command());

        Assert.Equal(0, response.PrunedOutboxCount);
        await using var verify = NewContext();
        Assert.Equal(2, await verify.OutboxMessages.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Old_Processed_Inbox_Rows_Are_Pruned_But_Recent_Ones_Survive()
    {
        await EnsureSchemaAsync();
        var old = ProcessedMessage.Create("push:OLD");
        var recent = ProcessedMessage.Create("push:RECENT");

        await using (var ctx = NewContext())
        {
            ctx.Set<ProcessedMessage>().AddRange(old, recent);
            await ctx.CommitAsync(CancellationToken.None);
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE \"ProcessedMessages\" SET \"ProcessedAt\" = {0} WHERE \"MessageKey\" = {1}",
                DateTime.UtcNow.AddDays(-30), "push:OLD");
        }

        var response = await RunAsync(new PruneOutbox.Command());

        Assert.Equal(1, response.PrunedProcessedCount);
        await using var verify = NewContext();
        var survivor = Assert.Single(await verify.Set<ProcessedMessage>().ToListAsync());
        Assert.Equal("push:RECENT", survivor.MessageKey);
    }

    [Fact]
    public async Task An_Admin_Audit_Row_Is_Never_Touched()
    {
        await EnsureSchemaAsync();
        var audit = new AdminActionAudit
        {
            ActorId = "admin-1",
            ActorProfile = UserProfile.Administrator,
            Action = "order.refunded",
            ResourceType = "Order",
            ResourceId = "ORDER-1",
            Success = true,
        };

        await using (var ctx = NewContext())
        {
            ctx.AdminActionAudits.Add(audit);
            ctx.OutboxMessages.Add(DispatchedRow("receipt:OLD", DateTimeOffset.UtcNow.AddDays(-30)));
            await ctx.CommitAsync(CancellationToken.None);
        }

        await RunAsync(new PruneOutbox.Command());

        await using var verify = NewContext();
        Assert.Equal(1, await verify.AdminActionAudits.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task A_Disabled_Prune_Deletes_Nothing()
    {
        await EnsureSchemaAsync();
        await using (var ctx = NewContext())
        {
            ctx.OutboxMessages.Add(DispatchedRow("receipt:OLD", DateTimeOffset.UtcNow.AddDays(-30)));
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using var runCtx = NewContext();
        var handler = new PruneOutbox.Handler(
            new OutboxMessageRepository(runCtx),
            new ProcessedMessageRepository(runCtx),
            runCtx,
            new StubConfig { Enabled = false, DispatchedRetentionDays = 14, ProcessedRetentionDays = 14, BatchSize = 500 },
            NullLogger<PruneOutbox.Handler>.Instance);

        var result = await handler.Handle(new PruneOutbox.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.PrunedOutboxCount);
        await using var verify = NewContext();
        Assert.Equal(1, await verify.OutboxMessages.IgnoreQueryFilters().CountAsync());
    }

    private sealed class StubConfig : IOutboxRetentionConfig
    {
        public bool Enabled { get; set; }
        public int DispatchedRetentionDays { get; set; }
        public int ProcessedRetentionDays { get; set; }
        public int BatchSize { get; set; }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
