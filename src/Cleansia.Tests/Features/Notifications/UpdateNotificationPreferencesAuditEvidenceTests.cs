using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Notifications;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Notifications;

/// <summary>
/// ADR-0062 D3 tier 2 at the producer: a preferences write emits ONE
/// <see cref="UpdateNotificationPreferences.NotificationPreferencesEvidence"/> with the eleven flags
/// before and after — the "I was never told" defence. A first write against no row records the defaults
/// as the before.
/// </summary>
public sealed class UpdateNotificationPreferencesAuditEvidenceTests
{
    private const string UserId = "user-1";

    private readonly Mock<IUserNotificationPreferencesRepository> _repository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly AuditContext _auditContext = new();

    public UpdateNotificationPreferencesAuditEvidenceTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
    }

    private UpdateNotificationPreferences.Handler CreateHandler() => new(_repository.Object, _session.Object, _auditContext);

    private static UpdateNotificationPreferences.Command AllOnExceptPromoAnd(bool orderCancelled) => new(
        OrderUpdates: true, CleanerOnTheWay: true, OrderCompleted: true, OrderCancelled: orderCancelled, RefundIssued: true,
        MembershipExpiring: true, MembershipCancelled: true, TierUpgrade: true, Promo: false, DisputeReply: true, RecurringScheduled: true);

    [Fact]
    public void The_Marker_Is_Frozen_On_The_User()
    {
        var descriptor = AuditActionDescriptor.For(typeof(UpdateNotificationPreferences.Command));

        Assert.Equal("customer.notification_preferences.update", descriptor.Action);
        Assert.Equal("User", descriptor.ResourceType);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.False(descriptor.AllowsAnonymousActor);
    }

    [Fact]
    public async Task Switching_A_Category_Off_Records_It_On_Before_And_Off_After()
    {
        _repository
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserNotificationPreferences.CreateDefaults(UserId));

        var result = await CreateHandler().Handle(AllOnExceptPromoAnd(orderCancelled: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal(UserId, snapshot.ResourceId);
        var payload = JsonDocument.Parse(snapshot.AfterJson!).RootElement;
        Assert.True(payload.GetProperty("before").GetProperty("orderCancelled").GetBoolean());
        Assert.False(payload.GetProperty("after").GetProperty("orderCancelled").GetBoolean());
        Assert.Equal(11, payload.GetProperty("before").EnumerateObject().Count());
        Assert.Equal(11, payload.GetProperty("after").EnumerateObject().Count());
        Assert.All(payload.GetProperty("after").EnumerateObject(), p => Assert.True(p.Value.ValueKind is JsonValueKind.True or JsonValueKind.False));
    }

    [Fact]
    public async Task A_First_Write_Records_The_Defaults_As_The_Before()
    {
        _repository
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserNotificationPreferences?)null);

        var result = await CreateHandler().Handle(AllOnExceptPromoAnd(orderCancelled: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var before = JsonDocument.Parse(_auditContext.DrainSnapshot()!.AfterJson!).RootElement.GetProperty("before");
        var defaults = UserNotificationPreferences.CreateDefaults(UserId);
        Assert.Equal(defaults.OrderUpdates, before.GetProperty("orderUpdates").GetBoolean());
        Assert.Equal(defaults.Promo, before.GetProperty("promo").GetBoolean());
        _repository.Verify(r => r.Add(It.IsAny<UserNotificationPreferences>()), Times.Once);
    }
}
