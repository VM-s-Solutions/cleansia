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
/// ADR-0002 D3.3 — the failure-classification split on
/// <see cref="SendPushNotificationHandler"/>, which previously THREW ON EVERYTHING (a permanent
/// deserialize/validation failure burned all 5 retries then poison-queued a message that can never
/// succeed). Post-fix:
///   • a MALFORMED / business-rejected body → logged at Warning and ACKED (return, NO throw);
///   • an INFRA / TRANSIENT fault → THROWN (so the runtime retries up to maxDequeueCount, then poisons,
///     where the -poison consumer durably records it).
///
/// The pre-existing return-on-missing-field guards (UserId/EventKey empty, muted category, no eligible
/// devices) MUST be preserved — they already ack.
///
/// Test-first (RED until the catch is split). Mirrors the GenerateReceipt classification idiom.
/// </summary>
public class SendPushNotificationClassifyTests
{
    private readonly Mock<IDeviceRepository> _deviceRepository = new();
    private readonly Mock<IUserNotificationPreferencesRepository> _preferencesRepository = new();
    private readonly Mock<IPushDispatcher> _pushDispatcher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly NoopIdempotencyGuard _guard = new();

    private SendPushNotificationHandler CreateHandler() => new(
        _deviceRepository.Object,
        _preferencesRepository.Object,
        _pushDispatcher.Object,
        _unitOfWork.Object,
        _guard,
        _tenantProvider.Object,
        NullLogger<SendPushNotificationHandler>.Instance);

    // A guard that never reports already-processed — so the classification branches under test (which
    // are about deserialize/validation/infra, not dedup) run their full path uninterrupted.
    private sealed class NoopIdempotencyGuard : IIdempotencyGuard
    {
        public Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(false);
    }

    private static string Serialize(SendPushNotificationMessage message) =>
        JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static string SerializeEnvelope(SendPushNotificationMessage message, string messageKey, string? tenantId) =>
        JsonSerializer.Serialize(
            new QueueEnvelope<SendPushNotificationMessage>(messageKey, tenantId, message),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    // ── ADR-0002 D2.1a envelope DUAL-READ ─────────────────────────────────
    // Producers wrap every push in QueueEnvelope<T>; the consumer previously deserialized the BARE
    // type, so UserId/EventKey nested under "payload" bound to null → "Discarding push message with
    // missing UserId" → silent ack. Every transactional push was dropped while CI stayed green
    // (tests only fed bare bodies). These pin the dual-read.

    [Fact]
    public async Task Enveloped_Body_Is_Unwrapped_And_Dispatched_Not_Discarded()
    {
        var handler = CreateHandler();

        // The real wire shape today: {"messageKey","tenantId","payload":{...}}.
        var body = SerializeEnvelope(
            new SendPushNotificationMessage(
                UserId: "USER-1", EventKey: "order.confirmed", Args: new(), TenantId: "TENANT-A"),
            messageKey: "push:USER-1:order.confirmed",
            tenantId: "TENANT-A");

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
            .ReturnsAsync(new PushDispatchResult(SuccessCount: 1, FailureCount: 0, InvalidTokens: new List<string>()));

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(body, CancellationToken.None));

        Assert.Null(ex);
        // The payload WAS unwrapped: the device lookup ran for the enveloped UserId, and the push was sent.
        _deviceRepository.Verify(r => r.GetByUserIdAsync("USER-1", It.IsAny<CancellationToken>()), Times.Once);
        _pushDispatcher.Verify(p => p.SendAsync(
            It.IsAny<IReadOnlyList<string>>(), "order.confirmed",
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
        // The envelope's TenantId is authoritative for the cross-tenant override.
        _tenantProvider.Verify(t => t.SetTenantOverride("TENANT-A"), Times.Once);
    }

    [Fact]
    public async Task Bare_Body_Is_Still_Processed_Backward_Compatible()
    {
        var handler = CreateHandler();

        // In-flight pre-envelope message — must still be processed, not discarded.
        var body = Serialize(new SendPushNotificationMessage(
            UserId: "USER-2", EventKey: "order.confirmed", Args: new(), TenantId: "TENANT-B"));

        _preferencesRepository
            .Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cleansia.Core.Domain.Notifications.UserNotificationPreferences?)null);
        _deviceRepository
            .Setup(r => r.GetByUserIdAsync("USER-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Cleansia.Core.Domain.Devices.Device>());

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(body, CancellationToken.None));

        Assert.Null(ex);
        _deviceRepository.Verify(r => r.GetByUserIdAsync("USER-2", It.IsAny<CancellationToken>()), Times.Once);
        // Bare body has no envelope tenant → override comes from the payload TenantId.
        _tenantProvider.Verify(t => t.SetTenantOverride("TENANT-B"), Times.Once);
    }

    // ── PERMANENT failures ACK (no throw) ──────────────────────────────────────────

    [Fact]
    public async Task Malformed_Body_That_Cannot_Deserialize_Acks_Without_Throwing()
    {
        var handler = CreateHandler();

        // Not valid JSON for the message — a permanent deserialize failure. Previously this threw
        // (throw-on-everything) and poison-queued an un-fixable message. Now it must ack.
        var ex = await Record.ExceptionAsync(() => handler.HandleAsync("}{ not json", CancellationToken.None));

        Assert.Null(ex);
        // Permanent failure never reaches the device lookup.
        _deviceRepository.Verify(
            r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Empty_Json_Object_Deserializing_To_Missing_Fields_Acks_Without_Throwing()
    {
        var handler = CreateHandler();

        // Valid JSON, but missing UserId/EventKey — the pre-existing missing-field guard acks. Preserved.
        var ex = await Record.ExceptionAsync(() => handler.HandleAsync("{}", CancellationToken.None));

        Assert.Null(ex);
        _deviceRepository.Verify(
            r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── INFRA / TRANSIENT faults THROW (so the runtime retries → poison) ────────────

    [Fact]
    public async Task Infra_Fault_During_Device_Lookup_Throws_So_Runtime_Retries()
    {
        var handler = CreateHandler();

        var message = Serialize(new SendPushNotificationMessage(
            UserId: "USER-1", EventKey: "order.confirmed", Args: new(), TenantId: null));

        // Simulated infra/transient fault — the DB read blows up. This MUST propagate so the queue
        // retries up to maxDequeueCount; acking here would silently drop recoverable work.
        _preferencesRepository
            .Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cleansia.Core.Domain.Notifications.UserNotificationPreferences?)null);
        _deviceRepository
            .Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("transient DB connection failure"));

        await Assert.ThrowsAsync<TimeoutException>(() => handler.HandleAsync(message, CancellationToken.None));
    }

    [Fact]
    public async Task Infra_Fault_During_Push_Send_Throws_So_Runtime_Retries()
    {
        var handler = CreateHandler();

        var message = Serialize(new SendPushNotificationMessage(
            UserId: "USER-1", EventKey: "order.confirmed", Args: new(), TenantId: null));

        _preferencesRepository
            .Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cleansia.Core.Domain.Notifications.UserNotificationPreferences?)null);
        _deviceRepository
            .Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Cleansia.Core.Domain.Devices.Device>
            {
                Cleansia.Core.Domain.Devices.Device.Create(
                    userId: "USER-1", platform: "android", deviceToken: "TOKEN-1", deviceId: "DEV-1"),
            });
        // FCM/network blows up — transient. Must throw.
        _pushDispatcher
            .Setup(p => p.SendAsync(
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("FCM unreachable"));

        await Assert.ThrowsAsync<HttpRequestException>(() => handler.HandleAsync(message, CancellationToken.None));
    }
}
