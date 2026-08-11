namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// ADR-0036 D5.1 — decides whether a customer's preferred cleaner may be told about their booking, and
/// whether the order's first seat is withheld from the rest of the board for them. Third sibling of
/// <see cref="ICancellationPolicyResolver"/>: a pure read that returns a record the caller acts on.
///
/// <para>It never assigns anyone, never writes either hold column (the aggregate owns that pair) and
/// never enqueues anything.</para>
/// </summary>
public interface IPreferredCleanerHoldResolver
{
    /// <summary>
    /// PURE READ. Safe to call from the quote path and from the factory.
    /// </summary>
    /// <param name="serviceCountryId">
    /// The country of the order's service address. A cleaner whose work country differs is one
    /// <c>TakeOrder</c> would refuse, so a hold for them is pure latency and so is a push.
    /// </param>
    /// <param name="estimatedMinutes">
    /// The booking's own length, so the slot-conflict question is asked about the real appointment
    /// window. Server-derived — a client-supplied duration deciding a server answer is an S1 violation,
    /// and a tampered one silently un-greys a busy cleaner.
    /// </param>
    Task<PreferredCleanerOutcome> ResolveAsync(
        string? userId,
        string? preferredEmployeeId,
        string? serviceCountryId,
        DateTime cleaningUtc,
        int estimatedMinutes,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// TWO outcomes, not one (ADR-0036 D4.1): the notification is granted on a WIDER predicate than the
/// hold. INVARIANT, one-way — <c>HoldUntilUtc != null ⇒ NotifyPreferred</c>. "No signal ⇒ no hold"
/// survives; its converse does not.
///
/// <para><see cref="Reason"/> carries WHY, so the decision is loggable and testable rather than a bare
/// null.</para>
/// </summary>
public record PreferredCleanerOutcome(
    bool NotifyPreferred,
    DateTime? HoldUntilUtc,
    HoldDeclineReason Reason,
    PreferredCleanerRecipient? Recipient = null)
{
    public static PreferredCleanerOutcome Declined(HoldDeclineReason reason) => new(false, null, reason);

    /// <summary>The weaker half of the perk: a signal we can act on, in a window we cannot hold.</summary>
    public static PreferredCleanerOutcome NotifyOnly(HoldDeclineReason reason, PreferredCleanerRecipient recipient) =>
        new(true, null, reason, recipient);

    public static PreferredCleanerOutcome Granted(DateTime holdUntilUtc, PreferredCleanerRecipient recipient) =>
        new(true, holdUntilUtc, HoldDeclineReason.None, recipient);
}

/// <summary>
/// Who the targeted push is addressed to, resolved here because the resolver has already loaded the
/// cleaner and because a hold whose beneficiary cannot be signalled is pure board latency. Both factory
/// methods that set <c>NotifyPreferred</c> require one, so the D4.1 invariant is carried by the type
/// rather than by remembering. <c>TenantId</c> is the RECIPIENT's, not the order's — it scopes the
/// downstream dispatch consumer, exactly as the digest's per-cleaner queue message does.
/// </summary>
public record PreferredCleanerRecipient(string UserId, string? TenantId);

public enum HoldDeclineReason
{
    None = 0,
    NoPreference = 1,
    NoMembership = 2,

    /// <summary>Too little lead time to withhold a seat. Notify: YES. Hold: no.</summary>
    ShortLeadTime = 3,

    CleanerNotApproved = 4,
    CleanerCountryMismatch = 5,
    CleanerMutedNewJobs = 6,
    CleanerNotFound = 7,
    CleanerUnreachableForPush = 8,

    /// <summary>
    /// ADR-0039. The cleaner already holds a live-commitment assignment overlapping
    /// <c>[cleaningUtc, cleaningUtc + estimatedMinutes)</c>, or we could not find out. Notify: NO.
    /// Hold: NO — short lead means we cannot hold; busy means they cannot take.
    /// </summary>
    CleanerBusyAtCleaningTime = 9,
}
