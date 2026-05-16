namespace Cleansia.Core.Domain.Notifications;

/// <summary>
/// Push-notification category — one toggle per category lives on
/// <see cref="UserNotificationPreferences"/>. The full set is defined here
/// (not just the MVP three) so adding more triggers in later phases is a
/// pure code change rather than a second migration.
///
/// Numeric values are pinned: do NOT renumber once shipped — they're
/// indirectly stored on UserNotificationPreferences columns and would
/// silently flip semantics if reshuffled. Append new values only.
/// </summary>
public enum NotificationCategory
{
    /// <summary>Cleaner accepted the booking; status: New → Confirmed.</summary>
    OrderUpdates = 1,

    /// <summary>Cleaner pressed "I'm on my way" in the partner app.</summary>
    CleanerOnTheWay = 2,

    /// <summary>Cleaning finished; status: InProgress → Completed.</summary>
    OrderCompleted = 3,

    /// <summary>Cleaner cancelled the order after accepting it.</summary>
    OrderCancelled = 4,

    /// <summary>Refund issued by Stripe webhook (PaymentStatus → Refunded).</summary>
    RefundIssued = 5,

    /// <summary>Cleansia Plus subscription renews in 3 days.</summary>
    MembershipExpiring = 6,

    /// <summary>Cleansia Plus cancellation effective tomorrow.</summary>
    MembershipCancelled = 7,

    /// <summary>Loyalty tier upgrade (Regular → Silver, etc.).</summary>
    TierUpgrade = 8,

    /// <summary>Sitewide promo code issued by the marketing team.</summary>
    Promo = 9,

    /// <summary>Support replied to one of the user's disputes.</summary>
    DisputeReply = 10,

    /// <summary>Recurring booking auto-scheduled and lands in 24h.</summary>
    RecurringScheduled = 11,
}
