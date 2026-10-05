using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Result of <see cref="IReferralService.ValidateAsync"/>. When
/// <see cref="IsValid"/> is true, <see cref="ReferrerUserId"/> identifies the
/// inviter (so the UI can render their first name); when false,
/// <see cref="Error"/> holds the rejection reason.
/// </summary>
public record ReferralValidateResult(
    bool IsValid,
    string? ReferrerUserId,
    ReferralValidationError? Error);

/// <summary>
/// Result of <see cref="IReferralService.AcceptAsync"/>. Acceptance is
/// fail-soft: callers (Register / CreateOrder) surface the error via
/// logging rather than blocking the user.
/// </summary>
public record ReferralAcceptResult(bool IsAccepted, ReferralValidationError? Error);

/// <summary>
/// Why a referral attempt was rejected. Stringified as the error code in
/// the <c>ValidateReferral</c> response — clients map to i18n keys.
/// </summary>
public enum ReferralValidationError
{
    NotFound,
    SelfReferral,
    AlreadyReferred,
    Inactive,
}

public interface IReferralService
{
    /// <summary>
    /// Get-or-create the user's lifetime referral code. Idempotent. Generates
    /// a fresh 6-char uppercase code with a collision-retry loop on first call.
    /// </summary>
    Task<ReferralCode> EnsureCodeForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Validate a code against the acceptance rules: existence, active,
    /// not self-referral, invitee hasn't already accepted one. Used by the
    /// signup form (<c>acceptingUserId</c> empty) and the booking-time late-
    /// acceptance path. Empty <c>acceptingUserId</c> skips the user-scoped
    /// checks (SelfReferral, AlreadyReferred) — those re-validate at accept.
    /// </summary>
    Task<ReferralValidateResult> ValidateAsync(
        string code, string acceptingUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Record acceptance. Validates first; on success, creates the
    /// <see cref="Referral"/> row in <see cref="ReferralStatus.Accepted"/>
    /// state. Caller commits via UoW.
    /// </summary>
    Task<ReferralAcceptResult> AcceptAsync(
        string code, string acceptingUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Called from <c>CompleteOrder.Handler</c>. If the user has a pending
    /// (Accepted, not held) referral and this is their first completed order within
    /// the qualifying window, either holds it for an administrator — when the two
    /// accounts share an address, a phone or an inbox — or credits each side the
    /// <c>ReferralCredit</c> of the currency it books in and flips the referral to
    /// Qualified. Idempotent — safe to call twice for the same orderId.
    /// </summary>
    Task ProcessOrderCompletedAsync(string orderId, string? userId, CancellationToken cancellationToken);

    /// <summary>
    /// Issue the inviter the <c>ReferralCredit</c> of <paramref name="referrerCurrency"/> and the invited
    /// friend that of <paramref name="referredCurrency"/>, one ledger row per side under a per-referral key.
    /// Answers what each side received: null for a side whose currency is null or has no positive figure,
    /// for an erased side, and for one whose account is on a company frozen for archive. Holds the paid
    /// owners' credit locks until the unit of work commits.
    /// </summary>
    Task<(decimal? ToReferrer, decimal? ToReferred)> AwardCreditAsync(
        Referral referral,
        Currency? referrerCurrency,
        Currency? referredCurrency,
        string? orderId,
        string actorId,
        string? note,
        CancellationToken cancellationToken);

    /// <summary>
    /// The currency a customer books in: that of their most recent order in any status, with any company.
    /// Null for a customer who has never booked.
    /// </summary>
    Task<Currency?> GetBookingCurrencyAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Background sweep — flip Referrals past the 90-day window from
    /// Accepted to Expired. No credit granted; cosmetic data hygiene only.
    /// </summary>
    Task ExpireStaleReferralsAsync(CancellationToken cancellationToken);
}
