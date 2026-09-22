using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Notifications;

/// <summary>
/// Which host's feed a request serves. Always set server-side by the host controller —
/// never trusted from the client — so a dual-role user's customer app can never read, count, or
/// mark-read the partner feed's rows (and vice versa), and an administrator's console never reads
/// a partner-app row of the same person.
/// </summary>
[SwaggerEnumAsInt]
public enum NotificationFeedAudience
{
    Customer = 0,
    Partner = 1,
    Admin = 2,
}

/// <summary>
/// The per-audience keysets for the notifications feed. Every feed operation is scoped to the calling
/// host's keyset.
///
/// <para><b>A key belongs in a keyset only once the audience's clients render it.</b> The unread badge
/// counts every row in the keyset, so a key listed ahead of its client template inflates the badge with
/// a row the app drops unrendered. → /architecture/push-notifications#event-catalogue</para>
/// </summary>
public static class NotificationFeedEventKeys
{
    public static readonly IReadOnlyList<string> Customer =
    [
        NotificationEventCatalog.OrderPaymentConfirmed,
        NotificationEventCatalog.OrderCleanerAssigned,
        NotificationEventCatalog.OrderOnTheWay,
        NotificationEventCatalog.OrderInProgress,
        NotificationEventCatalog.OrderCompleted,
        NotificationEventCatalog.OrderCancelled,
        NotificationEventCatalog.OrderRefunded,
        NotificationEventCatalog.OrderNoCleanerRefunded,
        NotificationEventCatalog.DisputeReply,
        NotificationEventCatalog.RecurringScheduled,
        NotificationEventCatalog.RecurringPaused,
        NotificationEventCatalog.MembershipExpiringSoon,
        NotificationEventCatalog.MembershipCancellationEffective,
        NotificationEventCatalog.LoyaltyTierUpgrade,
    ];

    public static readonly IReadOnlyList<string> Partner =
    [
        NotificationEventCatalog.NewJobsAvailable,
        NotificationEventCatalog.OrderSeatOpen,
        // The day-ahead digest only. The T-2h and T-30m reminders are deliberately push-only, like
        // the customer's own order.starting_soon, which is in neither keyset: they are transient, and
        // a feed row per job would fill the badge with things that have already happened.
        NotificationEventCatalog.ReminderTomorrow,
        NotificationEventCatalog.PreferredOffer,
        NotificationEventCatalog.OrderAssignmentCancelled,
        NotificationEventCatalog.InvoicePaid,
    ];

    /// <summary>
    /// The console is built to render every key of its catalogue, so the keyset is the catalogue by
    /// construction rather than a list that trails the client's templates. These rows are written by
    /// the admin notifier alone — never by the push seam, which is why <see cref="IsFeedEvent"/> does
    /// not know them.
    /// </summary>
    public static readonly IReadOnlyList<string> Admin = AdminNotificationEventCatalog.All;

    public static IReadOnlyList<string> For(NotificationFeedAudience audience) => audience switch
    {
        NotificationFeedAudience.Customer => Customer,
        NotificationFeedAudience.Partner => Partner,
        NotificationFeedAudience.Admin => Admin,
        _ => throw new ArgumentOutOfRangeException(nameof(audience), audience, "Unknown feed audience."),
    };

    public static bool IsFeedEvent(string eventKey) =>
        Customer.Contains(eventKey) || Partner.Contains(eventKey);
}
