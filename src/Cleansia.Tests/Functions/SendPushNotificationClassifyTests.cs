using System.Text.Json;
using Cleansia.Core.Clients.Abstractions.Fcm;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// TC-CLASSIFY-0 (ADR-0002 D3.3 / verify #9 / T-0120 AC4) — the failure-classification split on
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

    private SendPushNotificationHandler CreateHandler() => new(
        _deviceRepository.Object,
        _preferencesRepository.Object,
        _pushDispatcher.Object,
        _unitOfWork.Object,
        _tenantProvider.Object,
        NullLogger<SendPushNotificationHandler>.Instance);

    private static string Serialize(SendPushNotificationMessage message) =>
        JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    // ── AC4 — PERMANENT failures ACK (no throw) ──────────────────────────────────────────

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

    // ── AC4 — INFRA / TRANSIENT faults THROW (so the runtime retries → poison) ────────────

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
