using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Notifications;

/// <summary>
/// Per-user toggles for push-notification categories. One row per User —
/// lazy-created on first GET if missing (defaults: all-true).
///
/// Stored as discrete bool columns rather than a flags-int so:
///  - The schema is self-documenting in the DB.
///  - Adding a new <see cref="NotificationCategory"/> is a column add (not
///    a value renumber), keeping <see cref="NotificationCategory"/> safe to
///    extend without touching existing rows.
///  - Backfill defaults explicitly per column.
///
/// All defaults are TRUE. The only category we might consider opt-in by
/// default is <see cref="NotificationCategory.Promo"/> (marketing) — the
/// spec calls for opt-in there, so the column default is FALSE.
/// </summary>
public class UserNotificationPreferences : Auditable, ITenantEntity
{
    public string UserId { get; private set; } = default!;
    public virtual User User { get; private set; } = default!;

    public bool OrderUpdates { get; private set; } = true;
    public bool CleanerOnTheWay { get; private set; } = true;
    public bool OrderCompleted { get; private set; } = true;
    public bool OrderCancelled { get; private set; } = true;
    public bool RefundIssued { get; private set; } = true;
    public bool MembershipExpiring { get; private set; } = true;
    public bool MembershipCancelled { get; private set; } = true;
    public bool TierUpgrade { get; private set; } = true;

    /// <summary>Marketing — opt-in. Default false.</summary>
    public bool Promo { get; private set; }

    public bool DisputeReply { get; private set; } = true;
    public bool RecurringScheduled { get; private set; } = true;

    private UserNotificationPreferences() { }

    public static UserNotificationPreferences CreateDefaults(string userId)
    {
        return new UserNotificationPreferences { UserId = userId };
    }

    /// <summary>
    /// Read the bool toggle for a given category. Used by the dispatch
    /// Function to filter recipients by their preferences.
    /// </summary>
    public bool IsAllowed(NotificationCategory category) => category switch
    {
        NotificationCategory.OrderUpdates => OrderUpdates,
        NotificationCategory.CleanerOnTheWay => CleanerOnTheWay,
        NotificationCategory.OrderCompleted => OrderCompleted,
        NotificationCategory.OrderCancelled => OrderCancelled,
        NotificationCategory.RefundIssued => RefundIssued,
        NotificationCategory.MembershipExpiring => MembershipExpiring,
        NotificationCategory.MembershipCancelled => MembershipCancelled,
        NotificationCategory.TierUpgrade => TierUpgrade,
        NotificationCategory.Promo => Promo,
        NotificationCategory.DisputeReply => DisputeReply,
        NotificationCategory.RecurringScheduled => RecurringScheduled,
        _ => false,
    };

    public void Set(NotificationCategory category, bool enabled)
    {
        switch (category)
        {
            case NotificationCategory.OrderUpdates: OrderUpdates = enabled; break;
            case NotificationCategory.CleanerOnTheWay: CleanerOnTheWay = enabled; break;
            case NotificationCategory.OrderCompleted: OrderCompleted = enabled; break;
            case NotificationCategory.OrderCancelled: OrderCancelled = enabled; break;
            case NotificationCategory.RefundIssued: RefundIssued = enabled; break;
            case NotificationCategory.MembershipExpiring: MembershipExpiring = enabled; break;
            case NotificationCategory.MembershipCancelled: MembershipCancelled = enabled; break;
            case NotificationCategory.TierUpgrade: TierUpgrade = enabled; break;
            case NotificationCategory.Promo: Promo = enabled; break;
            case NotificationCategory.DisputeReply: DisputeReply = enabled; break;
            case NotificationCategory.RecurringScheduled: RecurringScheduled = enabled; break;
        }
    }
}
