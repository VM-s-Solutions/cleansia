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
/// T-0182 — FCM-disabled must NOT masquerade as all-failed-transient. An unconfigured FCM returns
/// <c>PushDispatchResult(0, count, [])</c>, the SAME shape the handler throws on for "all failed
/// transient" — so in any env with FCM deliberately off + eligible devices, every transactional push
/// threw → retried to maxDequeueCount → dead-lettered, contradicting the documented dev / CI no-op. The
/// fix gives <see cref="PushDispatchResult.Skipped"/> a DISTINCT signal so the handler ACKS the disabled
/// case (no throw, no poison) while STILL throwing on the genuine cold-start init race / network all-fail.
///
/// Test-first (RED until PushDispatchResult.Skipped exists and the handler branches on it).
/// </summary>
public class SendPushNotificationDisabledAckTests
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

    private sealed class NoopIdempotencyGuard : IIdempotencyGuard
    {
        public Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(false);
        public Task<bool> HasProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(false);
        public Task MarkProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private static string Serialize(SendPushNotificationMessage message) =>
        JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

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
    }

    [Fact]
    public async Task Fcm_Disabled_With_Eligible_Devices_Acks_Without_Throwing()
    {
        var handler = CreateHandler();
        var message = Serialize(new SendPushNotificationMessage(
            UserId: "USER-1", EventKey: "order.confirmed", Args: new(), TenantId: null));

        SetupOneEligibleDevice("USER-1");

        // FCM unconfigured: the dispatcher signals Skipped (a deliberate no-op), DISTINCT from all-failed.
        _pushDispatcher
            .Setup(p => p.SendAsync(
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushDispatchResult(SuccessCount: 0, FailureCount: 1, InvalidTokens: [], Skipped: true));

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(message, CancellationToken.None));

        Assert.Null(ex); // ACKED — no throw, no poison loop.
        // The dispatcher WAS called (correct SendAsync semantics) — the handler reached the send, then
        // recognized the skip and acked rather than short-circuiting before the send.
        _pushDispatcher.Verify(p => p.SendAsync(
            It.IsAny<IReadOnlyList<string>>(), "order.confirmed",
            It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
        // No dead-token prune commit on a skipped dispatch.
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cold_Start_Init_Race_All_Failed_Not_Skipped_Throws_So_The_Queue_Retries()
    {
        var handler = CreateHandler();
        var message = Serialize(new SendPushNotificationMessage(
            UserId: "USER-2", EventKey: "order.confirmed", Args: new(), TenantId: null));

        SetupOneEligibleDevice("USER-2");

        // The genuine cold-start FCM-init race / network all-fail: all-failed with no prunable token and
        // NOT skipped — must still throw (the BLIND-8 transient guard) so the queue redelivers.
        _pushDispatcher
            .Setup(p => p.SendAsync(
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushDispatchResult(SuccessCount: 0, FailureCount: 1, InvalidTokens: [], Skipped: false));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(message, CancellationToken.None));
    }
}
