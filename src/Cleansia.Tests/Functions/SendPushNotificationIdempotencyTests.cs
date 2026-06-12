using System.Text.Json;
using Cleansia.Core.Clients.Abstractions.Fcm;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// ADR-0002 D2.2 — guard-first (claim-then-act) idempotency on the <c>notifications-dispatch</c>
/// consumer. FCM is non-transactional and push has no domain target-state, so before
/// <see cref="IPushDispatcher.SendAsync"/> the consumer claims the deterministic D2.1 key via
/// <see cref="IIdempotencyGuard"/>; a redelivery / duplicate enqueue collapses onto the same key and
/// short-circuits. The guarantee is AT-MOST-ONCE AFTER THE MARKER (a crash between the claim and the
/// FCM send loses that one push — accepted for a notification, never a fiscal artifact).
///
/// Test-first (RED until the guard is wired into <see cref="SendPushNotificationHandler"/>).
/// Verify-gate items #5 (TC-IDEMP-0) and #6 (TC-KEY-0) for T-0182.
/// </summary>
public class SendPushNotificationIdempotencyTests
{
    private readonly Mock<IDeviceRepository> _deviceRepository = new();
    private readonly Mock<IUserNotificationPreferencesRepository> _preferencesRepository = new();
    private readonly Mock<IPushDispatcher> _pushDispatcher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly FakeIdempotencyGuard _guard = new();

    private SendPushNotificationHandler CreateHandler() => new(
        _deviceRepository.Object,
        _preferencesRepository.Object,
        _pushDispatcher.Object,
        _unitOfWork.Object,
        _guard,
        _tenantProvider.Object,
        NullLogger<SendPushNotificationHandler>.Instance);

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string SerializeEnvelope(SendPushNotificationMessage message, string messageKey, string? tenantId) =>
        JsonSerializer.Serialize(
            new QueueEnvelope<SendPushNotificationMessage>(messageKey, tenantId, message), JsonOptions);

    private void SetupOneEligibleDevice(string userId)
    {
        _preferencesRepository
            .Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cleansia.Core.Domain.Notifications.UserNotificationPreferences?)null);
        _deviceRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Cleansia.Core.Domain.Devices.Device>
            {
                Cleansia.Core.Domain.Devices.Device.Create(
                    userId: userId, platform: "android", deviceToken: "TOKEN-1", deviceId: "DEV-1"),
            });
        _pushDispatcher
            .Setup(p => p.SendAsync(
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushDispatchResult(SuccessCount: 1, FailureCount: 0, InvalidTokens: new List<string>()));
    }

    // ── TC-IDEMP-0 — safe to run twice (at-most-once after marker) ──────────────────

    [Fact]
    public async Task Same_Envelope_Delivered_Twice_Sends_Push_Exactly_Once()
    {
        SetupOneEligibleDevice("USER-1");
        var handler = CreateHandler();
        var body = SerializeEnvelope(
            new SendPushNotificationMessage(
                UserId: "USER-1", EventKey: "order.confirmed", Args: new(), TenantId: "TENANT-A"),
            messageKey: MessageKeys.Push("USER-1", "order.confirmed", "ORDER-1"),
            tenantId: "TENANT-A");

        await handler.HandleAsync(body, CancellationToken.None);
        await handler.HandleAsync(body, CancellationToken.None);

        _pushDispatcher.Verify(p => p.SendAsync(
            It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Second_Delivery_Short_Circuits_On_The_Claim_Before_Device_Lookup()
    {
        SetupOneEligibleDevice("USER-1");
        var handler = CreateHandler();
        var body = SerializeEnvelope(
            new SendPushNotificationMessage(
                UserId: "USER-1", EventKey: "order.confirmed", Args: new(), TenantId: null),
            messageKey: MessageKeys.Push("USER-1", "order.confirmed", "ORDER-1"),
            tenantId: null);

        await handler.HandleAsync(body, CancellationToken.None);
        await handler.HandleAsync(body, CancellationToken.None);

        // The second run claims the same key (already-processed) and returns BEFORE touching any
        // tenant-scoped read — the device lookup ran only on the first delivery.
        _deviceRepository.Verify(
            r => r.GetByUserIdAsync("USER-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Claim_Is_Taken_Before_The_Push_Is_Sent_GuardFirst()
    {
        // Guard-first ordering: the key is claimed strictly before SendAsync. We assert that when the
        // claim is already held (a prior delivery won it), no push is sent at all on this run.
        _guard.PreClaim(MessageKeys.Push("USER-1", "order.confirmed", "ORDER-1"));
        SetupOneEligibleDevice("USER-1");
        var handler = CreateHandler();
        var body = SerializeEnvelope(
            new SendPushNotificationMessage(
                UserId: "USER-1", EventKey: "order.confirmed", Args: new(), TenantId: null),
            messageKey: MessageKeys.Push("USER-1", "order.confirmed", "ORDER-1"),
            tenantId: null);

        await handler.HandleAsync(body, CancellationToken.None);

        _pushDispatcher.Verify(p => p.SendAsync(
            It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── TC-KEY-0 — the claim uses the envelope's deterministic D2.1 key ─────────────

    [Fact]
    public async Task Claim_Uses_The_Envelope_Deterministic_MessageKey()
    {
        SetupOneEligibleDevice("USER-1");
        var handler = CreateHandler();
        var key = MessageKeys.Push("USER-1", "order.confirmed", "ORDER-7");
        var body = SerializeEnvelope(
            new SendPushNotificationMessage(
                UserId: "USER-1", EventKey: "order.confirmed", Args: new(), TenantId: null),
            messageKey: key,
            tenantId: null);

        await handler.HandleAsync(body, CancellationToken.None);

        Assert.Contains(key, _guard.ClaimedKeys);
    }

    [Fact]
    public void Push_Key_Is_Deterministic_For_Same_Domain_Inputs()
    {
        // TC-KEY-0: same inputs => same key (no Guid/timestamp). The frozen D2.1 formula.
        Assert.Equal(
            MessageKeys.Push("USER-1", "order.confirmed", "ORDER-1"),
            MessageKeys.Push("USER-1", "order.confirmed", "ORDER-1"));
    }

    // ── AC5 — a transient-init all-failed no longer masquerades as acked ─────────────

    [Fact]
    public async Task All_Failed_With_No_Prunable_Token_Is_Transient_And_Throws()
    {
        // FcmPushDispatcher returns PushDispatchResult(0, count, []) on a broad-catch / cold-start init
        // race. Previously the consumer logged "all-failed, nothing pruned" and ACKED, silently dropping
        // the event's pushes. Now that shape is surfaced as transient → throw → the queue redelivers.
        _preferencesRepository
            .Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cleansia.Core.Domain.Notifications.UserNotificationPreferences?)null);
        _deviceRepository
            .Setup(r => r.GetByUserIdAsync("USER-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Cleansia.Core.Domain.Devices.Device>
            {
                Cleansia.Core.Domain.Devices.Device.Create(
                    userId: "USER-1", platform: "android", deviceToken: "TOKEN-1", deviceId: "DEV-1"),
            });
        _pushDispatcher
            .Setup(p => p.SendAsync(
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushDispatchResult(SuccessCount: 0, FailureCount: 1, InvalidTokens: new List<string>()));

        var handler = CreateHandler();
        var body = SerializeEnvelope(
            new SendPushNotificationMessage("USER-1", "order.confirmed", new(), null),
            messageKey: MessageKeys.Push("USER-1", "order.confirmed", "ORDER-1"),
            tenantId: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(body, CancellationToken.None));
    }

    [Fact]
    public async Task Dead_Token_Prune_Path_Is_Preserved_And_Acks()
    {
        // A genuinely-invalid token (FCM dead-token code) is pruned and the message acks — the prune path
        // is untouched by the transient-init fix above (it carries a prunable token, so it is NOT all-
        // failed-with-nothing-to-prune).
        _preferencesRepository
            .Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cleansia.Core.Domain.Notifications.UserNotificationPreferences?)null);
        _deviceRepository
            .Setup(r => r.GetByUserIdAsync("USER-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Cleansia.Core.Domain.Devices.Device>
            {
                Cleansia.Core.Domain.Devices.Device.Create(
                    userId: "USER-1", platform: "android", deviceToken: "DEAD-TOKEN", deviceId: "DEV-1"),
            });
        _pushDispatcher
            .Setup(p => p.SendAsync(
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushDispatchResult(
                SuccessCount: 0, FailureCount: 1, InvalidTokens: new List<string> { "DEAD-TOKEN" }));

        var handler = CreateHandler();
        var body = SerializeEnvelope(
            new SendPushNotificationMessage("USER-1", "order.confirmed", new(), null),
            messageKey: MessageKeys.Push("USER-1", "order.confirmed", "ORDER-1"),
            tenantId: null);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(body, CancellationToken.None));

        Assert.Null(ex);
        _deviceRepository.Verify(r => r.Remove(It.IsAny<Cleansia.Core.Domain.Devices.Device>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // Test double for IIdempotencyGuard: a claim survives across the two invocations in this test
    // class (mirrors the singleton in-memory backing) so a redelivery short-circuits.
    private sealed class FakeIdempotencyGuard : IIdempotencyGuard
    {
        private readonly HashSet<string> _claimed = [];
        public IReadOnlyCollection<string> ClaimedKeys => _claimed;
        public void PreClaim(string key) => _claimed.Add(key);
        public Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(!_claimed.Add(messageKey));
    }
}
