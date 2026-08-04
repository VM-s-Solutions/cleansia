using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Memberships;

/// <summary>
/// A user's enrollment in a <see cref="MembershipPlan"/>. Backed by a Stripe
/// subscription — the local row is a mirror, with Stripe as the authoritative
/// source for billing state. Webhooks keep this in sync.
///
/// One user can have at most one active membership at a time. This invariant
/// is asserted in handler code (the request path's GetActiveForUserAsync guard
/// and the webhook's ProvisionFromCreatedEventAsync active-check) AND
/// backstopped at the database by a FILTERED partial unique
/// index on (TenantId, UserId) WHERE Status = Active
/// (UserMembershipEntityConfiguration). The index is filtered to Active so a
/// cancelled/expired membership plus a new active subscription is still
/// allowed — a full unique index would wrongly block that legitimate
/// re-subscribe-after-cancel case.
/// </summary>
public class UserMembership : Auditable, ITenantEntity
{
    [Required]
    public string UserId { get; private set; } = default!;
    public User User { get; private set; } = default!;

    [Required]
    public string MembershipPlanId { get; private set; } = default!;
    public MembershipPlan MembershipPlan { get; private set; } = default!;

    /// <summary>
    /// Stripe subscription id (<c>sub_...</c>). Used for webhook reconciliation
    /// and for "cancel my subscription" calls back to Stripe.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string StripeSubscriptionId { get; private set; } = default!;

    [Required]
    public MembershipStatus Status { get; private set; }

    /// <summary>Start of the current Stripe billing period. Mirrors Stripe.</summary>
    public DateTime CurrentPeriodStart { get; private set; }

    /// <summary>
    /// End of the current Stripe billing period, and the second half of
    /// <see cref="IsActive"/>: benefits stop the moment it passes.
    ///
    /// <para>It does <b>not</b> bound the express-waiver quota. That quota is two per CALENDAR month
    /// (owner ruling), counted on <c>(TenantId, UserId, BenefitKind, PeriodKey)</c> — deliberately not on
    /// this row, so it carries across a mid-month plan switch and does not reset on re-subscribe.</para>
    ///
    /// <para>There is no grace window. <c>PastDue</c> is not <see cref="MembershipStatus.Active"/>, so
    /// every benefit stops on the first payment failure (owner ruling 2026-08-03).</para>
    /// </summary>
    public DateTime CurrentPeriodEnd { get; private set; }

    /// <summary>
    /// When the user requested cancellation. Subscription continues providing
    /// benefits through <see cref="CurrentPeriodEnd"/> then transitions to
    /// <see cref="MembershipStatus.Cancelled"/>. Null while active.
    /// </summary>
    public DateTime? CancelledAt { get; private set; }

    /// <summary>
    /// When the "your subscription renews soon" reminder push was dispatched
    /// for the current billing period. Reset to null whenever the period
    /// rolls over (in <see cref="UpdateFromStripeWebhook"/> / <see cref="ApplyPlanSwap"/>)
    /// so the sweep re-arms for the next period. The sweep filters by null
    /// inside a window of [now+2d, now+4d] to avoid double-firing.
    /// </summary>
    public DateTime? RenewalReminderSentAt { get; private set; }

    /// <summary>
    /// When the "your cancellation takes effect soon" reminder push was
    /// dispatched. Set once per cancellation request — cleared by
    /// <see cref="ApplyPlanSwap"/> because a plan swap implicitly retracts
    /// the pending cancellation. The sweep filters by null + CancelledAt
    /// != null inside [now, now+2d].
    /// </summary>
    public DateTime? CancellationReminderSentAt { get; private set; }

    /// <summary>
    /// End of the Stripe free trial, mirrored from the subscription's <c>trial_end</c>.
    /// NULL = this enrolment is not, and never was, in a trial.
    ///
    /// <para>Metered benefits (ADR-0035) are withheld while <c>UtcNow &lt; TrialEndsAtUtc</c>; the
    /// discount and the free-cancellation window are NOT (owner ruling 2026-08-03). Stripe flattens
    /// <c>"active"</c> and <c>"trialing"</c> onto <see cref="MembershipStatus.Active"/> in
    /// <see cref="UpdateFromStripeWebhook"/>, so without this column "is this member trialing?" has no
    /// answer in the database.</para>
    ///
    /// <para>A stored instant rather than a bool, deliberately: a bool needs a writer to flip it on
    /// conversion and no sweep exists, so it would go stale and grant waivers forever to anyone whose
    /// conversion webhook was missed. A deadline expires by clock with no actor.</para>
    ///
    /// <para>Read across ALL of a user's rows it is also the once-per-customer trial marker (owner ruling
    /// 2026-08-03) — see <c>IUserMembershipRepository.HasEverStartedTrialAsync</c>. That is why it is
    /// never cleared once set: <see cref="UpdateFromStripeWebhook"/> coalesces rather than assigns, so the
    /// <c>invoice.payment_failed</c> branch (an Invoice, carrying no <c>trial_end</c>) cannot erase it.</para>
    /// </summary>
    public DateTime? TrialEndsAtUtc { get; private set; }

    /// <summary>Computed; do not persist. Expires by clock — no sweep, no job, no state transition.</summary>
    public bool IsInTrial => TrialEndsAtUtc != null && DateTime.UtcNow < TrialEndsAtUtc;

    /// <summary>
    /// True when the membership is currently providing benefits — Active status
    /// AND we're still inside the paid period. Computed; do not persist. Used
    /// by the pricing pipeline + cancellation policy resolver to decide whether
    /// to apply membership benefits.
    /// </summary>
    public bool IsActive => Status == MembershipStatus.Active
        && DateTime.UtcNow < CurrentPeriodEnd;

    // Private constructor for EF Core
    private UserMembership() { }

    /// <summary>
    /// Create a new membership row from a freshly-created Stripe subscription.
    /// Caller is responsible for ensuring no other Active membership exists
    /// for this user.
    /// </summary>
    public static UserMembership Create(
        string userId,
        string membershipPlanId,
        string stripeSubscriptionId,
        DateTime currentPeriodStart,
        DateTime currentPeriodEnd,
        DateTime? trialEndsAtUtc = null)
        => new()
        {
            UserId = userId,
            MembershipPlanId = membershipPlanId,
            StripeSubscriptionId = stripeSubscriptionId,
            Status = MembershipStatus.Active,
            CurrentPeriodStart = currentPeriodStart,
            CurrentPeriodEnd = currentPeriodEnd,
            TrialEndsAtUtc = trialEndsAtUtc,
        };

    /// <summary>
    /// Update local mirror from a Stripe subscription webhook. Pass the
    /// status string Stripe sent ("active"/"past_due"/"canceled"/"paused") and
    /// the new period bounds. Mapping unknown statuses to Cancelled is the
    /// safe default — better to drop benefits than to grant them in error.
    /// </summary>
    public UserMembership UpdateFromStripeWebhook(
        string stripeStatus,
        DateTime currentPeriodStart,
        DateTime currentPeriodEnd,
        DateTime? trialEndsAtUtc)
    {
        Status = stripeStatus switch
        {
            "active" or "trialing" => MembershipStatus.Active,
            "past_due" => MembershipStatus.PastDue,
            "paused" => MembershipStatus.Paused,
            _ => MembershipStatus.Cancelled,
        };
        // Detect period rollover so the renewal reminder re-arms for the
        // new billing period. Stripe sends a period-rolled webhook on each
        // renewal, which moves CurrentPeriodEnd forward; that's our cue
        // to clear the per-period idempotency stamp.
        if (currentPeriodEnd != CurrentPeriodEnd)
        {
            RenewalReminderSentAt = null;
        }
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        // Null means the event said nothing about the trial — invoice.payment_failed carries an Invoice,
        // which has no trial_end at all. Assigning it would clear the marker on a dunning event, handing
        // the customer both their withheld benefits and a second free trial (ADR-0035 AM-18). Stripe
        // keeps trial_end populated after a trial converts, so a genuine "no trial" subscription is the
        // only one that stays null here.
        TrialEndsAtUtc = trialEndsAtUtc ?? TrialEndsAtUtc;
        return this;
    }

    /// <summary>
    /// Stamp the renewal-reminder dispatch time. Idempotency for the
    /// <c>membership.expiring_soon</c> sweep — see
    /// <see cref="RenewalReminderSentAt"/>.
    /// </summary>
    public UserMembership MarkRenewalReminderSent(DateTime sentAtUtc)
    {
        RenewalReminderSentAt ??= sentAtUtc;
        return this;
    }

    /// <summary>
    /// Stamp the cancellation-effective reminder dispatch time. Idempotency
    /// for the <c>membership.cancellation_effective</c> sweep.
    /// </summary>
    public UserMembership MarkCancellationReminderSent(DateTime sentAtUtc)
    {
        CancellationReminderSentAt ??= sentAtUtc;
        return this;
    }

    /// <summary>
    /// Mark the user's request to cancel. Stripe is configured with
    /// <c>cancel_at_period_end=true</c>, so benefits continue through
    /// <see cref="CurrentPeriodEnd"/>. The webhook will eventually transition
    /// status to Cancelled when the period actually ends.
    /// </summary>
    public UserMembership MarkCancellationRequested()
    {
        CancelledAt = DateTime.UtcNow;
        return this;
    }

    /// <summary>
    /// Swap to a different plan after Stripe has prorated and updated the
    /// subscription. New plan id + the period bounds Stripe returned post-swap.
    /// Used by the monthly→yearly upgrade flow.
    /// </summary>
    public UserMembership ApplyPlanSwap(
        string newMembershipPlanId,
        DateTime currentPeriodStart,
        DateTime currentPeriodEnd)
    {
        MembershipPlanId = newMembershipPlanId;
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        // A swap clears any pending cancellation — the user just confirmed
        // they want to keep paying (just on a different cadence).
        CancelledAt = null;
        // Re-arm reminder stamps: the new period needs a fresh renewal
        // reminder, and the cancellation reminder is moot now that the
        // cancellation was retracted.
        RenewalReminderSentAt = null;
        CancellationReminderSentAt = null;
        return this;
    }
}
