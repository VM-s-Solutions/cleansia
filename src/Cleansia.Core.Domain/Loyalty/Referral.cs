using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Loyalty;

/// <summary>
/// One-per-(inviter, invitee) record of a referral relationship. Created in
/// <see cref="ReferralStatus.Accepted"/> state when the invitee redeems a
/// code (at signup or first booking); flips to <see cref="ReferralStatus.Qualified"/>
/// on the invitee's first completed order; flips to <see cref="ReferralStatus.Expired"/>
/// after the 90-day qualifying window if no order has been completed.
/// </summary>
public class Referral : Auditable, ITenantEntity
{
    [Required]
    public string ReferrerUserId { get; private set; } = default!;
    public User? Referrer { get; private set; }

    [Required]
    public string ReferredUserId { get; private set; } = default!;
    public User? Referred { get; private set; }

    [Required]
    public string ReferralCodeId { get; private set; } = default!;
    public ReferralCode? ReferralCode { get; private set; }

    [Required]
    public ReferralStatus Status { get; private set; }

    [Required]
    public DateTimeOffset AcceptedOn { get; private set; }

    public DateTimeOffset? FirstQualifyingOrderOn { get; private set; }

    [MaxLength(26)]
    public string? FirstQualifyingOrderId { get; private set; }
    public Order? FirstQualifyingOrder { get; private set; }

    public int? PointsAwardedToReferrer { get; private set; }

    public int? PointsAwardedToReferred { get; private set; }

    public DateTimeOffset? PointsAwardedOn { get; private set; }

    // Private constructor for EF Core
    private Referral() { }

    /// <summary>
    /// Create a new referral row in <see cref="ReferralStatus.Accepted"/>
    /// state. Caller (the service layer) is responsible for the upstream
    /// validations (self-referral, already-referred, code-active).
    /// </summary>
    public static Referral CreateAccepted(
        string referrerUserId,
        string referredUserId,
        string referralCodeId,
        string actorId)
    {
        if (string.IsNullOrWhiteSpace(referrerUserId))
        {
            throw new ArgumentException("ReferrerUserId is required", nameof(referrerUserId));
        }
        if (string.IsNullOrWhiteSpace(referredUserId))
        {
            throw new ArgumentException("ReferredUserId is required", nameof(referredUserId));
        }
        if (string.Equals(referrerUserId, referredUserId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Self-referral is forbidden", nameof(referredUserId));
        }
        if (string.IsNullOrWhiteSpace(referralCodeId))
        {
            throw new ArgumentException("ReferralCodeId is required", nameof(referralCodeId));
        }

        var referral = new Referral
        {
            ReferrerUserId = referrerUserId,
            ReferredUserId = referredUserId,
            ReferralCodeId = referralCodeId,
            Status = ReferralStatus.Accepted,
            AcceptedOn = DateTimeOffset.UtcNow,
        };
        referral.Created(actorId, DateTimeOffset.UtcNow);
        return referral;
    }

    /// <summary>
    /// Mark this referral as qualified after the invitee's first completed
    /// order. Records the order id and the symmetric point grants for the
    /// admin/audit trail. Idempotency is enforced upstream (caller checks
    /// <see cref="Status"/> before calling).
    /// </summary>
    public void MarkQualified(
        string firstQualifyingOrderId,
        int pointsToReferrer,
        int pointsToReferred,
        string actorId)
    {
        Status = ReferralStatus.Qualified;
        FirstQualifyingOrderOn = DateTimeOffset.UtcNow;
        FirstQualifyingOrderId = firstQualifyingOrderId;
        PointsAwardedToReferrer = pointsToReferrer;
        PointsAwardedToReferred = pointsToReferred;
        PointsAwardedOn = DateTimeOffset.UtcNow;
        Updated(actorId, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Mark this referral as expired (90-day window elapsed without a
    /// qualifying order). No points are granted.
    /// </summary>
    public void MarkExpired(string actorId)
    {
        Status = ReferralStatus.Expired;
        Updated(actorId, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Admin force-qualify of a legitimate referral stuck in Accepted, where no
    /// qualifying order is being recorded (so <see cref="FirstQualifyingOrderId"/>
    /// stays null — there is no Order to FK to). Records the symmetric grants for
    /// the audit trail. Idempotency is enforced upstream (caller checks
    /// <see cref="Status"/> before calling).
    /// </summary>
    public void ForceQualify(int pointsToReferrer, int pointsToReferred, string actorId)
    {
        Status = ReferralStatus.Qualified;
        FirstQualifyingOrderOn = DateTimeOffset.UtcNow;
        PointsAwardedToReferrer = pointsToReferrer;
        PointsAwardedToReferred = pointsToReferred;
        PointsAwardedOn = DateTimeOffset.UtcNow;
        Updated(actorId, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Admin reversal of a previously-Qualified referral. Flips the status to
    /// the terminal <see cref="ReferralStatus.Reversed"/>; the symmetric point
    /// grants recorded on the row (<see cref="PointsAwardedToReferrer"/> /
    /// <see cref="PointsAwardedToReferred"/>) are kept for the audit trail and
    /// clawed back by the caller through the loyalty manual-revoke path.
    /// Idempotency is enforced upstream (caller checks <see cref="Status"/>
    /// before calling).
    /// </summary>
    public void Reverse(string actorId)
    {
        Status = ReferralStatus.Reversed;
        Updated(actorId, DateTimeOffset.UtcNow);
    }
}
