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
/// A credential rejection (FCM answers 401/403 — disabled service-account key, missing
/// firebase.messaging OAuth scope, FCM API not enabled) arrives in the SAME
/// <c>PushDispatchResult(0, count, [])</c> shape as an all-failed-transient dispatch, so the handler
/// threw and the queue redelivered — ~15 FCM rejections plus 15-25 OAuth mints per notification,
/// every one ending in the poison queue, with the provider's actual error text replaced by a
/// synthesized "transient init/dispatch fault" message that named the wrong cause.
///
/// A 401 is HOST-WIDE: every push in the system is failing identically, so redelivery is
/// amplification, not recovery. <see cref="PushDispatchResult.AuthConfig"/> gives that case a DISTINCT
/// signal so the handler ACKS it with one alertable log carrying
/// <see cref="PushDispatchResult.FailureDetail"/>, while the genuine transient all-failed shape still
/// throws.
/// </summary>
public class SendPushNotificationAuthConfigAckTests
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

    private void SetupEligibleDevices(string userId, params string[] tokens)
    {
        _preferencesRepository
            .Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cleansia.Core.Domain.Notifications.UserNotificationPreferences?)null);
        _deviceRepository
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens
                .Select((token, i) => Cleansia.Core.Domain.Devices.Device.Create(
                    userId: userId, platform: "ios", deviceToken: token, deviceId: $"DEV-{i + 1}"))
                .ToList());
    }

    private void SetupDispatch(PushDispatchResult result) =>
        _pushDispatcher
            .Setup(p => p.SendAsync(
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    [Fact]
    public async Task Provider_Auth_Rejection_Acks_Without_Throwing()
    {
        var handler = CreateHandler();
        var message = Serialize(new SendPushNotificationMessage(
            UserId: "USER-1", EventKey: "order.confirmed", Args: new(), TenantId: null));

        SetupEligibleDevices("USER-1", "TOKEN-1");
        SetupDispatch(new PushDispatchResult(
            SuccessCount: 0,
            FailureCount: 1,
            InvalidTokens: [],
            AuthConfig: true,
            FailureDetail: "401 Unauthenticated: Request had invalid authentication credentials."));

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(message, CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Provider_Auth_Rejection_Never_Prunes_Device_Rows()
    {
        // The single most damaging thing this branch could get wrong: a 401 says nothing about the
        // tokens, so classifying it as a dead token would delete EVERY Device row in the database on
        // the next notification. Pin it.
        var handler = CreateHandler();
        var message = Serialize(new SendPushNotificationMessage(
            UserId: "USER-1", EventKey: "order.confirmed", Args: new(), TenantId: null));

        SetupEligibleDevices("USER-1", "TOKEN-1", "TOKEN-2", "TOKEN-3");
        SetupDispatch(new PushDispatchResult(
            SuccessCount: 0,
            FailureCount: 3,
            InvalidTokens: [],
            AuthConfig: true,
            FailureDetail: "403 PermissionDenied: Firebase Cloud Messaging API has not been used."));

        await handler.HandleAsync(message, CancellationToken.None);

        _deviceRepository.Verify(
            r => r.Remove(It.IsAny<Cleansia.Core.Domain.Devices.Device>()), Times.Never);
    }

    [Fact]
    public async Task All_Failed_Without_Auth_Flag_Still_Throws_For_Redelivery()
    {
        // Characterization pin: the genuine cold-start init race / network all-fail keeps its retry.
        var handler = CreateHandler();
        var message = Serialize(new SendPushNotificationMessage(
            UserId: "USER-1", EventKey: "order.confirmed", Args: new(), TenantId: null));

        SetupEligibleDevices("USER-1", "TOKEN-1");
        SetupDispatch(new PushDispatchResult(SuccessCount: 0, FailureCount: 1, InvalidTokens: []));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(message, CancellationToken.None));
    }
}
