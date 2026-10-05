using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Internationalization;
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
public class Referral : TenantAuditable
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

    /// <summary>The credit issued to the inviter on qualification; null when that side received none.</summary>
    public decimal? CreditAwardedToReferrer { get; private set; }

    /// <summary>The credit issued to the invited friend on qualification; null when that side received none.</summary>
    public decimal? CreditAwardedToReferred { get; private set; }

    /// <summary>The currency both grants are denominated in; null when neither side was paid.</summary>
    [MaxLength(26)]
    public string? CreditCurrencyId { get; private set; }
    public Currency? CreditCurrency { get; private set; }

    public DateTimeOffset? AwardedOn { get; private set; }

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
    /// order. Records the order id and the credit each side was issued for the
    /// admin/audit trail. Idempotency is enforced upstream (caller checks
    /// <see cref="Status"/> before calling).
    /// </summary>
    public void MarkQualified(
        string firstQualifyingOrderId,
        string? creditCurrencyId,
        decimal? creditToReferrer,
        decimal? creditToReferred,
        string actorId)
    {
        FirstQualifyingOrderId = firstQualifyingOrderId;
        Qualify(creditCurrencyId, creditToReferrer, creditToReferred, actorId);
    }

    /// <summary>
    /// Mark this referral as expired (90-day window elapsed without a
    /// qualifying order). No credit is granted.
    /// </summary>
    public void MarkExpired(string actorId)
    {
        Status = ReferralStatus.Expired;
        Updated(actorId, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Admin force-qualify of a legitimate referral stuck in Accepted, where no
    /// qualifying order is being recorded (so <see cref="FirstQualifyingOrderId"/>
    /// stays null — there is no Order to FK to). Records the credit each side was
    /// issued for the audit trail. Idempotency is enforced upstream (caller checks
    /// <see cref="Status"/> before calling).
    /// </summary>
    public void ForceQualify(
        string? creditCurrencyId,
        decimal? creditToReferrer,
        decimal? creditToReferred,
        string actorId)
    {
        Qualify(creditCurrencyId, creditToReferrer, creditToReferred, actorId);
    }

    private void Qualify(string? creditCurrencyId, decimal? creditToReferrer, decimal? creditToReferred, string actorId)
    {
        var now = DateTimeOffset.UtcNow;
        var awarded = creditToReferrer is not null || creditToReferred is not null;
        Status = ReferralStatus.Qualified;
        FirstQualifyingOrderOn = now;
        CreditCurrencyId = awarded ? creditCurrencyId : null;
        CreditAwardedToReferrer = creditToReferrer;
        CreditAwardedToReferred = creditToReferred;
        AwardedOn = awarded ? now : null;
        Updated(actorId, now);
    }

    /// <summary>
    /// Admin reversal of a previously-Qualified referral. Flips the status to
    /// the terminal <see cref="ReferralStatus.Reversed"/>; the grants recorded on
    /// the row (<see cref="CreditAwardedToReferrer"/> / <see cref="CreditAwardedToReferred"/>)
    /// are kept for the audit trail, and the caller takes the credit back from the
    /// ledger. Idempotency is enforced upstream (caller checks <see cref="Status"/>
    /// before calling).
    /// </summary>
    public void Reverse(string actorId)
    {
        Status = ReferralStatus.Reversed;
        Updated(actorId, DateTimeOffset.UtcNow);
    }
}
