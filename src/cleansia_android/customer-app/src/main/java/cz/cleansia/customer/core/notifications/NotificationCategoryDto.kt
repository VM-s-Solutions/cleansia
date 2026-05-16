package cz.cleansia.customer.core.notifications

/**
 * Mirror of the backend <c>NotificationCategory</c> enum. Keep numeric
 * values pinned in sync with `Cleansia.Core.Domain.Notifications.NotificationCategory`
 * — they're stored on the user's preferences row server-side and
 * renumbering would silently flip semantics.
 */
enum class NotificationCategoryDto(val value: Int) {
    OrderUpdates(1),
    CleanerOnTheWay(2),
    OrderCompleted(3),
    OrderCancelled(4),
    RefundIssued(5),
    MembershipExpiring(6),
    MembershipCancelled(7),
    TierUpgrade(8),
    Promo(9),
    DisputeReply(10),
    RecurringScheduled(11),
}
