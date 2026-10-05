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
/// after the 90-day qualifying window if no order has been completed. A first completed order
/// whose two accounts look like one person leaves it <see cref="ReferralStatus.Accepted"/> with
/// <see cref="HoldReasons"/> set: held for an administrator, never expired, never paid until released.
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

    /// <summary>The currency the inviter's grant is denominated in; null when that side received none.</summary>
    [MaxLength(26)]
    public string? ReferrerCreditCurrencyId { get; private set; }
    public Currency? ReferrerCreditCurrency { get; private set; }

    /// <summary>The currency the invited friend's grant is denominated in; null when that side received none.</summary>
    [MaxLength(26)]
    public string? ReferredCreditCurrencyId { get; private set; }
    public Currency? ReferredCreditCurrency { get; private set; }

    public DateTimeOffset? AwardedOn { get; private set; }

    /// <summary>
    /// Why the first completed order paid nothing: comma-joined <c>HoldReason*</c> slugs. Null when the
    /// referral was never held; kept as history once an administrator releases or rejects it.
    /// </summary>
    [MaxLength(32)]
    public string? HoldReasons { get; private set; }

    public const string HoldReasonAddress = "address";
    public const string HoldReasonPhone = "phone";
    public const string HoldReasonEmail = "email";

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
        string? referrerCurrencyId,
        decimal? creditToReferrer,
        string? referredCurrencyId,
        decimal? creditToReferred,
        string actorId)
    {
        FirstQualifyingOrderId = firstQualifyingOrderId;
        Qualify(referrerCurrencyId, creditToReferrer, referredCurrencyId, creditToReferred, actorId);
    }

    /// <summary>
    /// The first qualifying order completed, but the two accounts appear to be one person: nothing is
    /// paid, and the referral stays <see cref="ReferralStatus.Accepted"/> until an administrator releases
    /// it (<see cref="ForceQualify"/>) or rejects it (<see cref="Reverse"/>).
    /// </summary>
    public void HoldForReview(string qualifyingOrderId, string reasons, string actorId)
    {
        FirstQualifyingOrderId = qualifyingOrderId;
        HoldReasons = reasons;
        Updated(actorId, DateTimeOffset.UtcNow);
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
    /// Admin force-qualify of a legitimate referral stuck in Accepted, or the release of a held one.
    /// <see cref="FirstQualifyingOrderId"/> is left as it is: null when no qualifying order was recorded,
    /// the held order on a release. Records the credit each side was issued, and its currency, for the
    /// audit trail. Idempotency is enforced upstream (caller checks <see cref="Status"/> before calling).
    /// </summary>
    public void ForceQualify(
        string? referrerCurrencyId,
        decimal? creditToReferrer,
        string? referredCurrencyId,
        decimal? creditToReferred,
        string actorId)
    {
        Qualify(referrerCurrencyId, creditToReferrer, referredCurrencyId, creditToReferred, actorId);
    }

    private void Qualify(
        string? referrerCurrencyId,
        decimal? creditToReferrer,
        string? referredCurrencyId,
        decimal? creditToReferred,
        string actorId)
    {
        var now = DateTimeOffset.UtcNow;
        Status = ReferralStatus.Qualified;
        FirstQualifyingOrderOn = now;
        ReferrerCreditCurrencyId = creditToReferrer is null ? null : referrerCurrencyId;
        ReferredCreditCurrencyId = creditToReferred is null ? null : referredCurrencyId;
        CreditAwardedToReferrer = creditToReferrer;
        CreditAwardedToReferred = creditToReferred;
        AwardedOn = creditToReferrer is not null || creditToReferred is not null ? now : null;
        Updated(actorId, now);
    }

    /// <summary>
    /// Admin reversal of a previously-Qualified referral, or the rejection of a held one. Flips the status to
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
