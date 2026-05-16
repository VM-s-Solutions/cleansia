namespace Cleansia.Core.AppServices.Features.Notifications.DTOs;

/// <summary>
/// Per-user push-notification preferences. Mirrors the
/// <c>UserNotificationPreferences</c> entity 1:1 — one bool per category.
/// New categories appended here must also be appended on the entity, the
/// <c>NotificationCategory</c> enum, and the i18n strings.
/// </summary>
public record NotificationPreferencesDto(
    bool OrderUpdates,
    bool CleanerOnTheWay,
    bool OrderCompleted,
    bool OrderCancelled,
    bool RefundIssued,
    bool MembershipExpiring,
    bool MembershipCancelled,
    bool TierUpgrade,
    bool Promo,
    bool DisputeReply,
    bool RecurringScheduled);
