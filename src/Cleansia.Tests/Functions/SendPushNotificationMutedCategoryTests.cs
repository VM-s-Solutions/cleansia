using System.Text.Json;
using Cleansia.Core.Clients.Abstractions.Fcm;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// AC4 — the muted-category gate on SendPushNotificationHandler: when the user's preferences row exists
/// and the event's category is muted (IsAllowed == false), the consumer acks with NO device lookup and
/// NO dispatch. When the same category is allowed, the push goes out — proving the gate is the discriminator,
/// not an always-skip. The classify/idempotency suites cover everything around this branch but not it.
/// </summary>
public class SendPushNotificationMutedCategoryTests
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
    }

    private static string Serialize(SendPushNotificationMessage message) =>
        JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task Muted_Category_Acks_Without_Dispatching()
    {
        // order.completed maps to NotificationCategory.OrderCompleted; the user has muted it.
        var prefs = UserNotificationPreferences.CreateDefaults("USER-1");
        prefs.Set(NotificationCategory.OrderCompleted, false);
        _preferencesRepository
            .Setup(r => r.GetByUserIdAsync("USER-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);

        var body = Serialize(new SendPushNotificationMessage(
            UserId: "USER-1", EventKey: NotificationEventCatalog.OrderCompleted, Args: new(), TenantId: null));

        var ex = await Record.ExceptionAsync(() => CreateHandler().HandleAsync(body, CancellationToken.None));

        Assert.Null(ex);
        _deviceRepository.Verify(
            r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _pushDispatcher.Verify(
            p => p.SendAsync(
                It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Allowed_Category_Is_Dispatched_Proving_The_Gate_Is_The_Discriminator()
    {
        var prefs = UserNotificationPreferences.CreateDefaults("USER-1");
        prefs.Set(NotificationCategory.OrderCompleted, true);
        _preferencesRepository
            .Setup(r => r.GetByUserIdAsync("USER-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(prefs);
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
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushDispatchResult(SuccessCount: 1, FailureCount: 0, InvalidTokens: []));

        var body = Serialize(new SendPushNotificationMessage(
            UserId: "USER-1", EventKey: NotificationEventCatalog.OrderCompleted, Args: new(), TenantId: null));

        await CreateHandler().HandleAsync(body, CancellationToken.None);

        _pushDispatcher.Verify(
            p => p.SendAsync(
                It.IsAny<IReadOnlyList<string>>(), NotificationEventCatalog.OrderCompleted,
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
