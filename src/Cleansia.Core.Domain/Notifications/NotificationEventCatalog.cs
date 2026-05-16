namespace Cleansia.Core.Domain.Notifications;

/// <summary>
/// Single source of truth mapping event-keys (the strings flowing on the
/// queue + going into the FCM payload) to <see cref="NotificationCategory"/>
/// values (the per-user opt-in toggles).
///
/// Keep in sync with the <c>strings.xml</c> entries on the customer Android
/// app — the same keys are looked up there.
/// </summary>
public static class NotificationEventCatalog
{
    public const string OrderConfirmed = "order.confirmed";
    public const string OrderOnTheWay = "order.on_the_way";
    public const string OrderInProgress = "order.in_progress";
    public const string OrderCompleted = "order.completed";
    public const string OrderCancelled = "order.cancelled";
    public const string OrderRefunded = "order.refunded";
    public const string MembershipExpiringSoon = "membership.expiring_soon";
    public const string MembershipCancellationEffective = "membership.cancellation_effective";
    public const string LoyaltyTierUpgrade = "loyalty.tier_upgrade";
    public const string PromoNewSitewide = "promo.new_sitewide";
    public const string DisputeReply = "dispute.reply";
    public const string RecurringScheduled = "recurring.scheduled";

    public static NotificationCategory? GetCategoryFor(string eventKey) => eventKey switch
    {
        OrderConfirmed => NotificationCategory.OrderUpdates,
        OrderOnTheWay => NotificationCategory.CleanerOnTheWay,
        OrderInProgress => NotificationCategory.OrderUpdates,
        OrderCompleted => NotificationCategory.OrderCompleted,
        OrderCancelled => NotificationCategory.OrderCancelled,
        OrderRefunded => NotificationCategory.RefundIssued,
        MembershipExpiringSoon => NotificationCategory.MembershipExpiring,
        MembershipCancellationEffective => NotificationCategory.MembershipCancelled,
        LoyaltyTierUpgrade => NotificationCategory.TierUpgrade,
        PromoNewSitewide => NotificationCategory.Promo,
        DisputeReply => NotificationCategory.DisputeReply,
        RecurringScheduled => NotificationCategory.RecurringScheduled,
        _ => null,
    };
}
