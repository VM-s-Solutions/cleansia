using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Memberships;

/// <summary>
/// A user's enrollment in a <see cref="MembershipPlan"/>. Backed by a Stripe
/// subscription — the local row is a mirror, with Stripe as the authoritative
/// source for billing state. Webhooks keep this in sync.
///
/// One user can have at most one active membership at a time (enforced in
/// handler code, not by a unique index — cancelled+new is allowed and would
/// violate the index).
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
    /// End of the current Stripe billing period. Used by benefit usage
    /// tracking ("free express upgrade once per period") and by
    /// <see cref="IsActive"/> to gate benefits during the grace window.
    /// </summary>
    public DateTime CurrentPeriodEnd { get; private set; }

    /// <summary>
    /// When the user requested cancellation. Subscription continues providing
    /// benefits through <see cref="CurrentPeriodEnd"/> then transitions to
    /// <see cref="MembershipStatus.Cancelled"/>. Null while active.
    /// </summary>
    public DateTime? CancelledAt { get; private set; }

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
        DateTime currentPeriodEnd)
        => new()
        {
            UserId = userId,
            MembershipPlanId = membershipPlanId,
            StripeSubscriptionId = stripeSubscriptionId,
            Status = MembershipStatus.Active,
            CurrentPeriodStart = currentPeriodStart,
            CurrentPeriodEnd = currentPeriodEnd,
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
        DateTime currentPeriodEnd)
    {
        Status = stripeStatus switch
        {
            "active" or "trialing" => MembershipStatus.Active,
            "past_due" => MembershipStatus.PastDue,
            "paused" => MembershipStatus.Paused,
            _ => MembershipStatus.Cancelled,
        };
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
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
        return this;
    }
}
