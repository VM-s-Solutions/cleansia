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
    /// (Accepted) referral and this is their first completed order within
    /// the qualifying window, grants +150 to both sides and flips the
    /// referral to Qualified. Idempotent — safe to call twice for the same
    /// orderId.
    /// </summary>
    Task ProcessOrderCompletedAsync(string orderId, string? userId, CancellationToken cancellationToken);

    /// <summary>
    /// Background sweep — flip Referrals past the 90-day window from
    /// Accepted to Expired. No points granted; cosmetic data hygiene only.
    /// </summary>
    Task ExpireStaleReferralsAsync(CancellationToken cancellationToken);
}
