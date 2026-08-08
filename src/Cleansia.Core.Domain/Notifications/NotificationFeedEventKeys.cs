using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Notifications;

/// <summary>
/// Which mobile host's feed a request serves. Always set server-side by the host controller —
/// never trusted from the client — so a dual-role user's customer app can never read, count, or
/// mark-read the partner feed's rows (and vice versa).
/// </summary>
[SwaggerEnumAsInt]
public enum NotificationFeedAudience
{
    Customer = 0,
    Partner = 1,
}

/// <summary>
/// The per-audience keysets for the notifications feed — the single source of truth beside
/// <see cref="NotificationEventCatalog"/>. Every feed operation (paged list, unread count,
/// mark-read, mark-all-read) is scoped to the calling host's keyset.
///
/// Customer = every event currently dispatched to customers EXCEPT <c>promo.new_sitewide</c>
/// (the one event with server-authored literal text; excluded from feed v1). Partner = every
/// partner-targeted dispatch.
///
/// A key belongs in a keyset only once the audience's clients render it: the unread badge counts
/// every row in the keyset, so a key listed ahead of its client template inflates the badge with a
/// row the app drops unrendered.
/// </summary>
public static class NotificationFeedEventKeys
{
    public static readonly IReadOnlyList<string> Customer =
    [
        NotificationEventCatalog.OrderConfirmed,
        NotificationEventCatalog.OrderCleanerAssigned,
        NotificationEventCatalog.OrderOnTheWay,
        NotificationEventCatalog.OrderInProgress,
        NotificationEventCatalog.OrderCompleted,
        NotificationEventCatalog.OrderCancelled,
        NotificationEventCatalog.OrderRefunded,
        NotificationEventCatalog.DisputeReply,
        NotificationEventCatalog.RecurringScheduled,
        NotificationEventCatalog.MembershipExpiringSoon,
        NotificationEventCatalog.MembershipCancellationEffective,
        NotificationEventCatalog.LoyaltyTierUpgrade,
    ];

    public static readonly IReadOnlyList<string> Partner =
    [
        NotificationEventCatalog.NewJobsAvailable,
        NotificationEventCatalog.PreferredOffer,
        NotificationEventCatalog.OrderAssignmentCancelled,
        NotificationEventCatalog.InvoicePaid,
    ];

    public static IReadOnlyList<string> For(NotificationFeedAudience audience) => audience switch
    {
        NotificationFeedAudience.Customer => Customer,
        NotificationFeedAudience.Partner => Partner,
        _ => throw new ArgumentOutOfRangeException(nameof(audience), audience, "Unknown feed audience."),
    };

    public static bool IsFeedEvent(string eventKey) =>
        Customer.Contains(eventKey) || Partner.Contains(eventKey);
}
