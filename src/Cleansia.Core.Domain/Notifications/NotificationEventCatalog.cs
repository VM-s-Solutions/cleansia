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

    /// <summary>
    /// Customer-targeted: a cleaner is now committed to this order. Produced wherever an assignment
    /// row is created — <c>TakeOrder</c> and <c>AdminReassignOrder</c>. Args: <c>orderId</c> (deep
    /// link) + <c>orderNumber</c> (loc); no cleaner name, which belongs on the order detail the deep
    /// link opens rather than on a lock screen.
    ///
    /// <para>Distinct from <see cref="OrderConfirmed"/> on purpose. <c>Confirmed</c> is overloaded —
    /// "money settled" OR "cleaner assigned" — and two of <see cref="OrderConfirmed"/>'s producers
    /// (the Stripe webhook, the recurring cash confirmation) have no cleaner, so widening that key to
    /// carry this claim would repeat the overloading one layer up.</para>
    /// </summary>
    public const string OrderCleanerAssigned = "order.cleaner_assigned";

    /// <summary>
    /// Customer-targeted: the cleaning this customer booked starts in about an hour. The promise the
    /// booking-confirmation screen makes ("We'll remind you 1 hour before") — entirely within the
    /// platform's control, and until now made by nothing. Args: <c>orderNumber</c> (loc) +
    /// <c>orderId</c> (deep link).
    ///
    /// <para>One-off orders only. A recurring occurrence's reminder is
    /// <see cref="RecurringScheduled"/>, which is a different message at a different lead time — it
    /// asks the customer to CONFIRM 24h out, where this one only tells them the day has come.</para>
    ///
    /// <para>Mutable under <see cref="NotificationCategory.OrderUpdates"/> rather than a category of
    /// its own: a new category is a bool COLUMN on <c>UserNotificationPreferences</c> plus a toggle in
    /// every client, and a customer who silenced order updates has already said what they want.</para>
    /// </summary>
    public const string OrderStartingSoon = "order.starting_soon";

    /// <summary>
    /// Customer-targeted: the cleaner this customer asked for did not take the booking, and the order is
    /// back with the whole board (ADR-0045 D6). Args: <c>orderNumber</c> (loc) + <c>orderId</c> (deep
    /// link). Produced by exactly two callers through one notifier — the 5-minute lapse sweep and the
    /// cleaner's explicit decline — so the sentence and its args are byte-identical on both paths.
    ///
    /// <para><b>One sentence covers both outcomes, and that is the whole of the guarantee.</b> The
    /// customer learns that the offer ended and is offered a second choice; they are never told that a
    /// named person refused, and never told that a named person did not answer. A per-path string would
    /// re-introduce exactly the disclosure the neutral line exists to prevent, and
    /// <c>Q-AVAIL-04</c> — which lawful basis covers telling a third party what a worker did — is
    /// open.</para>
    ///
    /// <para>Mutable under <see cref="NotificationCategory.OrderUpdates"/>: an update about the
    /// customer's own order must not need its own opt-out to be discoverable, and someone who silenced
    /// order updates has already answered the question.</para>
    /// </summary>
    public const string PreferredOfferClosed = "order.preferred_offer_closed";

    /// <summary>
    /// Partner-side digest. Args: <c>count</c> (decimal-string count of new
    /// eligible orders). Body localized client-side ("N new jobs near you").
    ///
    /// <para>The client copy says "near you", and the server now means it: the count is the cleaner's
    /// work country narrowed by their own <c>Employee.JobRadiusKm</c> around their home address
    /// (<see cref="Orders.JobProximity"/>). <b>The copy is still not true for everyone</b> — a cleaner
    /// who has set no radius, and one whose home never geocoded, both keep the country-wide board by
    /// design, and the args carry no way for the client to tell those apart. Making the wording follow
    /// the reality needs a second loc arg or a second event key, i.e. new strings in both apps.</para>
    /// </summary>
    public const string NewJobsAvailable = "order.new_available";

    /// <summary>
    /// Partner-targeted: a customer this cleaner has worked for before asked for them by name on a new
    /// booking (ADR-0036 D4). One order, not a count — args: <c>orderNumber</c> (loc) + <c>orderId</c>
    /// (deep link). It bypasses the digest cadence entirely and does NOT stamp the digest watermark:
    /// the hold's length has to be set by the customer's tolerance for latency, not by our sweep
    /// interval, and a second writer on the watermark would suppress the cleaner's next digest of OTHER
    /// jobs. Mutable under <see cref="NotificationCategory.NewJobsAvailable"/> — a cleaner who silenced
    /// new-job notifications must not receive a push-shaped bypass of that mute.
    ///
    /// <para>This is the only place the platform tells a cleaner they were chosen. No surface ever says
    /// an order is held for SOMEONE ELSE, and no cleaner ever learns they were passed over.</para>
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
}
