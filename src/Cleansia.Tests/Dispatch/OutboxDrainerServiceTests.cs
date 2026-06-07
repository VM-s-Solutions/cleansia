using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// TC-OUTBOX-DRAIN-0 / TC-OUTBOX-DEADLETTER-0 — the drainer's claim → send → mark loop. A claimed
/// row is sent via the unchanged <see cref="IQueueClient"/> and marked dispatched ONLY after a
/// successful send; a send failure leaves it claimable for retry (it is not lost); a row that
/// exhausts its retry budget is retired through the existing dead-letter path. The repository is
/// mocked so the loop's branching is exercised deterministically; the real claim/lease SQL is
/// covered against a live DbContext in <see cref="OutboxMessageRepositoryClaimTests"/>.
/// </summary>
public sealed class OutboxDrainerServiceTests
{
    private readonly Mock<IOutboxMessageRepository> _repository = new();
    private readonly Mock<IQueueClient> _queueClient = new();
    private readonly Mock<IDeadLetterStore> _deadLetterStore = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    private OutboxDrainerService NewService(int maxAttempts = 5) =>
        new(_repository.Object,
            _queueClient.Object,
            _deadLetterStore.Object,
            _tenantProvider.Object,
            new StubConfig { BatchSize = 100, MaxAttempts = maxAttempts, BaseBackoffSeconds = 30, LeaseSeconds = 120 },
            NullLogger<OutboxDrainerService>.Instance);

    private void Claimed(params OutboxMessage[] rows) =>
        _repository
            .Setup(r => r.ClaimPendingBatchAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

    private static OutboxMessage Row(string orderId) =>
        OutboxMessage.Create(QueueNames.GenerateReceipt, $"receipt:{orderId}", $"{{\"orderId\":\"{orderId}\"}}", "tenant-1");

    [Fact]
    public async Task A_Claimed_Row_Is_Sent_Verbatim_And_Marked_Dispatched()
    {
        var row = Row("ORDER-1");
        Claimed(row);

        var dispatched = await NewService().DrainOnceAsync(CancellationToken.None);

        Assert.Equal(1, dispatched);
        _queueClient.Verify(
            q => q.SendAsync(QueueNames.GenerateReceipt, It.Is<string>(b => b == row.Body), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(OutboxMessageStatus.Dispatched, row.Status);
        Assert.NotNull(row.DispatchedOn);
        _repository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Mark_Dispatched_Happens_Only_After_A_Successful_Send()
    {
        var row = Row("ORDER-1");
        Claimed(row);

        var statusAtSend = (OutboxMessageStatus?)null;
        _queueClient
            .Setup(q => q.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => statusAtSend = row.Status)
            .Returns(Task.CompletedTask);

        await NewService().DrainOnceAsync(CancellationToken.None);

        Assert.Equal(OutboxMessageStatus.Pending, statusAtSend);
        Assert.Equal(OutboxMessageStatus.Dispatched, row.Status);
    }

    [Fact]
    public async Task A_Send_Failure_Leaves_The_Row_Claimable_For_Retry()
    {
        var row = Row("ORDER-1");
        Claimed(row);
        _queueClient
            .Setup(q => q.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("azure unreachable"));

        var dispatched = await NewService(maxAttempts: 5).DrainOnceAsync(CancellationToken.None);

        Assert.Equal(0, dispatched);
        Assert.Equal(OutboxMessageStatus.Pending, row.Status);
        Assert.Equal(1, row.AttemptCount);
        Assert.NotNull(row.NextAttemptAt);
        Assert.Equal("azure unreachable", row.LastError);
        _deadLetterStore.Verify(
            d => d.RecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task One_Send_Failure_Does_Not_Block_The_Other_Rows_In_The_Batch()
    {
        var failing = Row("ORDER-FAIL");
        var ok = Row("ORDER-OK");
        Claimed(failing, ok);
        _queueClient
            .Setup(q => q.SendAsync(QueueNames.GenerateReceipt, failing.Body, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var dispatched = await NewService().DrainOnceAsync(CancellationToken.None);

        Assert.Equal(1, dispatched);
        Assert.Equal(OutboxMessageStatus.Pending, failing.Status);
        Assert.Equal(OutboxMessageStatus.Dispatched, ok.Status);
    }

    [Fact]
    public async Task A_Row_That_Exhausts_Its_Retry_Budget_Is_Failed_And_Dead_Lettered()
    {
        var row = Row("ORDER-1");
        Claimed(row);
        _queueClient
            .Setup(q => q.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("permanently broken"));

        await NewService(maxAttempts: 1).DrainOnceAsync(CancellationToken.None);

        Assert.Equal(OutboxMessageStatus.Failed, row.Status);
        Assert.Null(row.NextAttemptAt);
        _deadLetterStore.Verify(
            d => d.RecordAsync(QueueNames.GenerateReceipt, row.Body, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task An_Empty_Claim_Sends_Nothing()
    {
        Claimed();

        var dispatched = await NewService().DrainOnceAsync(CancellationToken.None);

        Assert.Equal(0, dispatched);
        _queueClient.Verify(
            q => q.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Each_Row_Is_Sent_Under_Its_Own_Tenant_Override()
    {
        var row = Row("ORDER-1");
        Claimed(row);

        await NewService().DrainOnceAsync(CancellationToken.None);

        _tenantProvider.Verify(t => t.SetTenantOverride("tenant-1"), Times.Once);
        _tenantProvider.Verify(t => t.ClearTenantOverride(), Times.AtLeastOnce);
    }

    private sealed class StubConfig : IOutboxDrainerConfig
    {
        public int BatchSize { get; set; }
        public int MaxAttempts { get; set; }
        public int BaseBackoffSeconds { get; set; }
        public int LeaseSeconds { get; set; }
    }
}
