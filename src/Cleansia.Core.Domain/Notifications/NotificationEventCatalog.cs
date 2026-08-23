namespace Cleansia.Core.Domain.Notifications;

/// <summary>
/// Single source of truth mapping event keys — the strings on the queue and in the FCM payload — to the
/// per-user opt-in category. <b>Keep in sync with the Android apps' string resources</b>; the same keys
/// are looked up there. → /architecture/push-notifications#event-catalogue
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

    /// <summary>
    /// Customer-targeted: a cleaner is now committed to this order. Produced wherever an assignment
    /// row is created — <c>TakeOrder</c> and <c>AdminReassignOrder</c>. Args: <c>orderId</c> (deep
    /// link) + <c>orderNumber</c> (loc); no cleaner name, which belongs on the order detail the deep
    /// link opens rather than on a lock screen.
    ///
    /// <para><b>Distinct from <see cref="OrderConfirmed"/> on purpose</b> — that key is overloaded and two
    /// of its producers have no cleaner at all.
    /// → /architecture/push-notifications#assigned-vs-confirmed</para>
    /// </summary>
    public const string OrderCleanerAssigned = "order.cleaner_assigned";

    /// <summary>
    /// Customer-targeted: the booked cleaning starts in about an hour — the promise the confirmation
    /// screen makes. <b>One-off orders only</b>; a recurring occurrence gets a different message at a
    /// different lead time, asking the customer to CONFIRM rather than telling them the day has come.
    /// Mutable under order updates. → /architecture/push-notifications#mutability
    /// </summary>
    public const string OrderStartingSoon = "order.starting_soon";

    /// <summary>
    /// Customer-targeted: the cleaner this customer asked for did not take the booking, and the order is
    /// back with the whole board (ADR-0045 D6). Args: <c>orderNumber</c> (loc) + <c>orderId</c> (deep
    /// link). Produced by exactly two callers through one notifier — the 5-minute lapse sweep and the
    /// cleaner's explicit decline — so the sentence and its args are byte-identical on both paths.
    ///
    /// <para><b>One sentence covers both outcomes, and that IS the guarantee.</b> The customer is never
    /// told a named person refused, and never told a named person did not answer. A per-path string
    /// reintroduces exactly the disclosure this line exists to prevent.
    /// → /architecture/push-notifications#one-sentence</para>
    /// </summary>
    public const string PreferredOfferClosed = "order.preferred_offer_closed";

    /// <summary>
    /// Partner-side digest. Args: <c>count</c> (decimal-string count of new
    /// eligible orders). Body localized client-side ("N new jobs near you").
    ///
    /// <para>Narrowed by the cleaner's own <c>Employee.JobRadiusKm</c>. <b>The "near you" copy is still
    /// not true for everyone</b> — no radius set, or a home that never geocoded, both keep the
    /// country-wide board. → /architecture/push-notifications#near-you</para>
    /// </summary>
    public const string NewJobsAvailable = "order.new_available";

    /// <summary>
    /// Partner-targeted: the evening before, how many jobs this cleaner has tomorrow. Args:
    /// <c>count</c> (decimal-string). Body localized client-side.
    ///
    /// <para>Sent at 18:00 in the CLEANER's local time, resolved from their
    /// <c>Employee.WorkCountryId</c> through <c>CountryConfiguration.TimeZoneId</c> — never from a
    /// client-supplied header, which is spoofable and banned on any path that decides anything.</para>
    ///
    /// <para><b>Not mutable.</b> Same reasoning as <see cref="OrderAssigned"/>: a cleaner must not be
    /// able to silence a job appearing on their own schedule and then not turn up. This one exists
    /// because they were forgetting. → /architecture/push-notifications#event-catalogue</para>
    /// </summary>
    public const string ReminderTomorrow = "order.reminder_tomorrow";

    /// <summary>
    /// Partner-targeted: this cleaner's job starts in about two hours. One order, not a count — args:
    /// <c>orderNumber</c> (loc) + <c>orderId</c> (deep link).
    ///
    /// <para>Per ASSIGNMENT, not per order: an order's crew is <c>ceil(EstimatedTime / 120)</c>, so a
    /// two-seat job sends two of these. Not mutable, for the reason above.</para>
    /// </summary>
    public const string ReminderSoon = "order.reminder_soon";

    /// <summary>
    /// Partner-targeted: the job starts shortly and this cleaner has not set off. Args:
    /// <c>orderNumber</c> (loc) + <c>orderId</c> (deep link).
    ///
    /// <para>The only one of the three with a precondition beyond the clock: it is suppressed for a
    /// cleaner already <c>OnTheWay</c> or <c>InProgress</c> on ANY assignment, not merely this one. That
    /// is what keeps it from being noise on a full day — back-to-back jobs put this window inside the
    /// previous job — and it is why this is the last reminder rather than a second copy of
    /// <see cref="ReminderSoon"/>. Not mutable.</para>
    /// </summary>
    public const string ReminderNotStarted = "order.reminder_not_started";

    /// <summary>
    /// Partner-targeted: a customer this cleaner has worked for before asked for them by name on a new
    /// booking (ADR-0036 D4). One order, not a count — args: <c>orderNumber</c> (loc) + <c>orderId</c>
    /// (deep link). It bypasses the digest cadence entirely and does NOT stamp the digest watermark:
    /// the hold's length has to be set by the customer's tolerance for latency, not by our sweep
    /// interval, and a second writer on the watermark would suppress the cleaner's next digest of OTHER
    /// jobs. Mutable under <see cref="NotificationCategory.NewJobsAvailable"/> — a cleaner who silenced
    /// new-job notifications must not receive a push-shaped bypass of that mute.
    ///
    /// <para>The only place the platform tells a cleaner they were chosen. <b>No surface ever says an
    /// order is held for SOMEONE ELSE, and no cleaner ever learns they were passed over.</b>
    /// → /architecture/push-notifications#one-sentence</para>
    /// </summary>
    public const string PreferredOffer = "order.preferred_offer";

    /// <summary>
    /// Partner-targeted: a job the cleaner ACCEPTED was cancelled (by the customer or an admin).
    /// A dedicated key, NOT the customer <c>order.cancelled</c>, so the audience feed keysets stay
    /// disjoint. Args: <c>orderNumber</c> (loc) + <c>orderId</c> (deep link). Non-mutable
    /// (GetCategoryFor returns null) — a cancellation must not be silenceable.
    /// </summary>
    public const string OrderAssignmentCancelled = "order.assignment_cancelled";

    /// <summary>
    /// Partner-targeted: an admin put this cleaner on a job the cleaner did not take themselves
    /// (<c>AdminReassignOrder</c> — the only assignment write with an actor other than the cleaner;
    /// <c>TakeOrder</c> is self-service and needs no notice). Args: <c>orderNumber</c> (loc) +
    /// <c>orderId</c> (deep link). Distinct from the customer-facing
    /// <see cref="OrderCleanerAssigned"/>, which the same handler emits to the other party — the two
    /// audiences' keysets are disjoint so a dual-role user's two apps cannot read each other's rows.
    /// Non-mutable (GetCategoryFor returns null) — a cleaner must not be able to silence a job
    /// appearing on their own schedule and then not turn up.
    /// </summary>
    public const string OrderAssigned = "order.assigned";

    /// <summary>
    /// Partner-targeted: an admin took this cleaner OFF a job they were assigned to
    /// (<c>AdminReassignOrder</c> with a replaced cleaner). Args: <c>orderNumber</c> (loc) +
    /// <c>orderId</c> (deep link). Deliberately NOT <see cref="OrderAssignmentCancelled"/>, whose copy
    /// states the job was cancelled: here the job goes ahead with somebody else, and a cleaner
    /// repeating our wording to the customer would be telling them their booking was cancelled.
    /// Non-mutable for the same reason as its counterpart above — losing a booked day is not an
    /// optional notice.
    /// </summary>
    public const string OrderAssignmentRevoked = "order.assignment_revoked";

    /// <summary>
    /// Partner-targeted: the cleaner's invoice for a pay period has been marked PAID. The
    /// "you've been paid" moment — the highest-value, most-actionable payroll signal. Args:
    /// <c>invoiceId</c> (deep link only; title/body are argless). Non-mutable (GetCategoryFor
    /// returns null) — a payment confirmation must not be silenceable.
    /// </summary>
    public const string InvoicePaid = "payroll.invoice_paid";

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
        OrderCleanerAssigned => NotificationCategory.OrderUpdates,
        OrderStartingSoon => NotificationCategory.OrderUpdates,
        PreferredOfferClosed => NotificationCategory.OrderUpdates,
        NewJobsAvailable => NotificationCategory.NewJobsAvailable,
        PreferredOffer => NotificationCategory.NewJobsAvailable,
        _ => null,
    };

    /// <summary>
    /// Feed events that carry a COUNT and must overwrite their own unread row rather than adding one.
    ///
    /// <para>The test is membership of this set, not equality with one key, because the property that
    /// earns the collapse is the payload's shape. Both members answer "how many, right now" and neither
    /// payload carries a date — so a stale row does not merely duplicate, it <b>lies</b>: a Monday
    /// evening's <c>reminder_tomorrow</c> row still reads <i>"Jobs tomorrow: 3"</i> when the cleaner
    /// opens the feed on Thursday. That is the argument, and it is a correctness one; the row count is
    /// a distant second and is bounded anyway by retention.</para>
    ///
    /// <para>The two per-job reminders are deliberately absent, and not by oversight — they are not
    /// feed events at all. Each is about one specific job at one specific time, so a second one is new
    /// information rather than a refreshed answer to the same question.</para>
    /// </summary>
    public static readonly IReadOnlySet<string> CollapsingDigestKeys =
        new HashSet<string>(StringComparer.Ordinal) { NewJobsAvailable, ReminderTomorrow };
}
