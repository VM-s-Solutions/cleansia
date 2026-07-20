using System.Text.Json;
using Cleansia.Core.Clients.Abstractions.Apns;
using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0029 D2 — the <c>live-activity-dispatch</c> consumer. Mirrors <c>SendPushNotificationHandler</c>:
/// guard-first idempotency, Skipped-ack when the provider is off, per-token send, dead-token prune, and
/// terminal (end) cleanup. TC-LA-3 (consumer half) / TC-LA-4 (terminal cleanup).
/// </summary>
public class SendLiveActivityUpdateHandlerTests
{
    private readonly Mock<ILiveActivityTokenRepository> _tokens = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ILiveActivityPushClient> _client = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly FakeIdempotencyGuard _guard = new();

    private SendLiveActivityUpdateHandler CreateHandler() => new(
        _tokens.Object, _orders.Object, _client.Object, _unitOfWork.Object,
        _guard, _tenantProvider.Object, NullLogger<SendLiveActivityUpdateHandler>.Instance);

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static SendLiveActivityUpdateMessage Message(string eventKey) => new(
        UserId: "USER-1", OrderId: "ORDER-1", EventKey: eventKey, OrderNumber: "ORD-1",
        ScheduledStart: DateTimeOffset.UtcNow, ScheduledEnd: DateTimeOffset.UtcNow.AddHours(2),
        TransitionAtUtc: DateTimeOffset.UtcNow, TenantId: "TENANT-A");

    private static string Envelope(SendLiveActivityUpdateMessage message, int sequence = 1) =>
        JsonSerializer.Serialize(
            new QueueEnvelope<SendLiveActivityUpdateMessage>(
                MessageKeys.LiveActivity(message.OrderId, message.EventKey, sequence), message.TenantId, message),
            JsonOptions);

    private static LiveActivityToken OrderToken() =>
        LiveActivityToken.Create("USER-1", "DEV-1", "ORDER-1", "APNS-TOKEN", "TENANT-A");

    private void SetupOrderTokens(params LiveActivityToken[] rows) =>
        _tokens.Setup(r => r.GetByUserAndOrderAsync("USER-1", "ORDER-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows.ToList());

    // ── happy path ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_Delivers_To_The_Order_Token_Once()
    {
        SetupOrderTokens(OrderToken());
        _client.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LiveActivityPushResult.Sent());

        await CreateHandler().HandleAsync(Envelope(Message(LiveActivityEventKeys.Update)), CancellationToken.None);

        _client.Verify(c => c.SendAsync("APNS-TOKEN", It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(r => r.Remove(It.IsAny<LiveActivityToken>()), Times.Never);
        _tokens.Verify(r => r.RemoveRange(It.IsAny<IEnumerable<LiveActivityToken>>()), Times.Never);
    }

    [Fact]
    public async Task Start_Targets_The_PushToStart_Tokens_Not_The_Order_Tokens()
    {
        _tokens.Setup(r => r.GetPushToStartTokensAsync("USER-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiveActivityToken> { LiveActivityToken.Create("USER-1", "DEV-1", null, "PTS-TOKEN", "TENANT-A") });
        _client.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LiveActivityPushResult.Sent());

        await CreateHandler().HandleAsync(Envelope(Message(LiveActivityEventKeys.Start)), CancellationToken.None);

        _client.Verify(c => c.SendAsync("PTS-TOKEN", It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(r => r.GetByUserAndOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── skip-when-disabled ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Provider_Disabled_Acks_Without_Throwing_Or_Deleting()
    {
        SetupOrderTokens(OrderToken());
        _client.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LiveActivityPushResult.SkippedResult());

        var ex = await Record.ExceptionAsync(
            () => CreateHandler().HandleAsync(Envelope(Message(LiveActivityEventKeys.End)), CancellationToken.None));

        Assert.Null(ex);
        _tokens.Verify(r => r.RemoveRange(It.IsAny<IEnumerable<LiveActivityToken>>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── skip-when-no-token ────────────────────────────────────────────────────────────

    [Fact]
    public async Task No_Tokens_Acks_Without_Calling_The_Client()
    {
        SetupOrderTokens();
        var ex = await Record.ExceptionAsync(
            () => CreateHandler().HandleAsync(Envelope(Message(LiveActivityEventKeys.Update)), CancellationToken.None));

        Assert.Null(ex);
        _client.Verify(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── idempotency (guard-first) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Same_Envelope_Twice_Sends_Exactly_Once()
    {
        SetupOrderTokens(OrderToken());
        _client.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LiveActivityPushResult.Sent());
        var handler = CreateHandler();
        var body = Envelope(Message(LiveActivityEventKeys.Update));

        await handler.HandleAsync(body, CancellationToken.None);
        await handler.HandleAsync(body, CancellationToken.None);

        _client.Verify(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Distinct_Sequences_Are_Distinct_Claims_And_Both_Send()
    {
        SetupOrderTokens(OrderToken());
        _client.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LiveActivityPushResult.Sent());
        var handler = CreateHandler();

        await handler.HandleAsync(Envelope(Message(LiveActivityEventKeys.Update), sequence: 4), CancellationToken.None);
        await handler.HandleAsync(Envelope(Message(LiveActivityEventKeys.Update), sequence: 5), CancellationToken.None);

        _client.Verify(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── dead-token prune ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invalid_Token_On_Update_Is_Pruned_And_Committed()
    {
        var token = OrderToken();
        SetupOrderTokens(token);
        _client.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LiveActivityPushResult.InvalidToken());

        await CreateHandler().HandleAsync(Envelope(Message(LiveActivityEventKeys.Update)), CancellationToken.None);

        _tokens.Verify(r => r.Remove(token), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── terminal (end) deactivation ─────────────────────────────────────────────────────

    [Fact]
    public async Task End_Deletes_The_Orders_Token_Rows_And_Commits()
    {
        var tokens = new[] { OrderToken(), OrderToken() };
        SetupOrderTokens(tokens);
        _client.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LiveActivityPushResult.Sent());

        await CreateHandler().HandleAsync(Envelope(Message(LiveActivityEventKeys.End)), CancellationToken.None);

        _tokens.Verify(r => r.RemoveRange(It.Is<IEnumerable<LiveActivityToken>>(rows => rows.Count() == 2)), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _orders.Verify(r => r.GetByIdAsync("ORDER-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_Does_Not_Fetch_The_Order()
    {
        SetupOrderTokens(OrderToken());
        _client.Setup(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LiveActivityPushResult.Sent());

        await CreateHandler().HandleAsync(Envelope(Message(LiveActivityEventKeys.Update)), CancellationToken.None);

        _orders.Verify(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── malformed body ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Malformed_Body_Is_Acked_As_Permanent()
    {
        var ex = await Record.ExceptionAsync(
            () => CreateHandler().HandleAsync("}{ not json", CancellationToken.None));

        Assert.Null(ex);
        _client.Verify(c => c.SendAsync(It.IsAny<string>(), It.IsAny<LiveActivityPush>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class FakeIdempotencyGuard : IIdempotencyGuard
    {
        private readonly HashSet<string> _claimed = [];
        public Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(!_claimed.Add(messageKey));
        public Task<bool> HasProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(_claimed.Contains(messageKey));
        public Task MarkProcessedAsync(string messageKey, CancellationToken ct = default)
        {
            _claimed.Add(messageKey);
            return Task.CompletedTask;
        }
    }
}
