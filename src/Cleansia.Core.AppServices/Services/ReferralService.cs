using System.Security.Cryptography;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// Coordinates referral-code generation + acceptance, and the symmetric +150
/// grants when an invitee completes their first order. Mirrors the
/// <see cref="LoyaltyService"/> shape: handlers call into this, the service
/// keeps the business rules.
/// </summary>
public sealed class ReferralService(
    IReferralCodeRepository referralCodeRepository,
    IReferralRepository referralRepository,
    IOrderRepository orderRepository,
    ILoyaltyService loyaltyService,
    IUnitOfWork unitOfWork,
    ILogger<ReferralService> logger) : IReferralService
{
    private const string SystemActor = "system";
    private const int CodeLength = 6;
    private const int MaxGenerationAttempts = 5;

    /// <summary>
    /// 28 chars: digits 2-9 (excluding look-alikes 0/1) + consonants only
    /// (excludes vowels A E I O U so codes can't accidentally form words,
    /// and excludes I/O which look like 1/0). 28^6 ≈ 481M combinations.
    /// </summary>
    private static readonly char[] CodeAlphabet =
        "BCDFGHJKLMNPQRSTVWXYZ23456789".ToCharArray();

    public async Task<ReferralCode> EnsureCodeForUserAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required", nameof(userId));
        }

        var existing = await referralCodeRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        // Generate with retry-on-collision. The codespace is huge (~481M)
        // so realistic collisions are vanishingly rare; 5 tries is paranoia
        // budget. Throwing on exhaustion is safe — caller can retry.
        for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var candidate = GenerateRandomCode(CodeLength);
            var collides = await referralCodeRepository.CodeExistsAsync(candidate, cancellationToken);
            if (!collides)
            {
                var rc = ReferralCode.Generate(userId, candidate, SystemActor);
                referralCodeRepository.Add(rc);
                // Commit here even though Ensure is typically called from a
                // Query handler (GetMyReferral). The UnitOfWork pipeline only
                // commits for Commands — without this explicit commit the new
                // row gets returned to the caller, displayed in the UI, then
                // discarded when the request scope ends. The invitee then
                // can't validate the code their friend shared because the
                // row never persisted. Safe to commit here because the only
                // pending change is the new ReferralCode itself.
                await unitOfWork.CommitAsync(cancellationToken);
                return rc;
            }
            logger.LogWarning(
                "ReferralCode collision on attempt {Attempt} for user {UserId} (code={Code}); retrying.",
                attempt + 1, userId, candidate);
        }

        throw new InvalidOperationException(
            $"Failed to generate unique referral code for user {userId} after {MaxGenerationAttempts} attempts.");
    }

    public async Task<ReferralValidateResult> ValidateAsync(
        string code, string acceptingUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new ReferralValidateResult(false, null, ReferralValidationError.NotFound);
        }

        var normalised = code.Trim().ToUpperInvariant();
        var referralCode = await referralCodeRepository.GetByCodeAsync(normalised, cancellationToken);
        if (referralCode == null)
        {
            return new ReferralValidateResult(false, null, ReferralValidationError.NotFound);
        }

        if (!referralCode.IsActive)
        {
            return new ReferralValidateResult(false, null, ReferralValidationError.Inactive);
        }

        // When called from the signup form the user isn't authenticated yet
        // — skip the user-scoped checks. They get re-evaluated server-side
        // inside AcceptAsync (which the Register handler calls AFTER the
        // user is created).
        if (string.IsNullOrEmpty(acceptingUserId))
        {
            return new ReferralValidateResult(true, referralCode.UserId, null);
        }

        if (string.Equals(referralCode.UserId, acceptingUserId, StringComparison.Ordinal))
        {
            return new ReferralValidateResult(false, null, ReferralValidationError.SelfReferral);
        }

        var existing = await referralRepository.GetByReferredUserIdAsync(acceptingUserId, cancellationToken);
        if (existing != null)
        {
            return new ReferralValidateResult(false, null, ReferralValidationError.AlreadyReferred);
        }

        return new ReferralValidateResult(true, referralCode.UserId, null);
    }

    public async Task<ReferralAcceptResult> AcceptAsync(
        string code, string acceptingUserId, CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(code, acceptingUserId, cancellationToken);
        if (!validation.IsValid)
        {
            return new ReferralAcceptResult(false, validation.Error);
        }

        // Re-fetch the code (the validate call already loaded it but didn't
        // return it). One extra query is cheap and keeps the API surface tight.
        var normalised = code.Trim().ToUpperInvariant();
        var referralCode = await referralCodeRepository.GetByCodeAsync(normalised, cancellationToken);
        if (referralCode == null || validation.ReferrerUserId == null)
        {
            // Defensive — should be unreachable given the IsValid check above.
            return new ReferralAcceptResult(false, ReferralValidationError.NotFound);
        }

        var referral = Referral.CreateAccepted(
            referrerUserId: validation.ReferrerUserId,
            referredUserId: acceptingUserId,
            referralCodeId: referralCode.Id,
            actorId: SystemActor);

        referralRepository.Add(referral);
        return new ReferralAcceptResult(true, null);
    }

    public async Task ProcessOrderCompletedAsync(
        string orderId, string? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            // Anonymous / legacy orders don't drive referrals.
            return;
        }

        var referral = await referralRepository.GetByReferredUserIdAsync(userId, cancellationToken);
        if (referral == null)
        {
            // User wasn't referred — nothing to do.
            return;
        }

        // Idempotency: if we've already qualified or expired, no work to do.
        // A second CompleteOrder fire for the same order would land here.
        if (referral.Status != ReferralStatus.Accepted)
        {
            return;
        }

        // 90-day qualifying window — if the invitee took too long, expire
        // the referral and grant nothing.
        var windowDeadline = referral.AcceptedOn.AddDays(ReferralPolicy.QualifyingWindowDays);
        if (DateTimeOffset.UtcNow > windowDeadline)
        {
            referral.MarkExpired(SystemActor);
            return;
        }

        // First-completed-order check. CompleteOrder.Handler has already
        // appended the OrderStatusTrack with Status=Completed by the time
        // this runs, so the count includes the current order. Anything
        // other than 1 means the user had earlier completed orders — they
        // don't qualify (the referral fires on the FIRST one only).
        var completedCount = await orderRepository.GetQueryable()
            .Where(o => o.UserId == userId
                && o.OrderStatusHistory.Any(h => h.Status == OrderStatus.Completed))
            .CountAsync(cancellationToken);

        if (completedCount != 1)
        {
            // Not the first qualifying order — leave the referral pending.
            // It will eventually expire if no future order qualifies it
            // (which is fine — only the first counts by design).
            return;
        }

        // Symmetric grant. Each call is idempotent on (orderId, Referral)
        // so a re-invocation here can't double-grant. The referral path keeps
        // its (orderId, source) key — requestId is null (the new idempotency
        // key is only for the admin manual path).
        await loyaltyService.GrantPointsManuallyAsync(
            referral.ReferrerUserId,
            ReferralPolicy.PointsPerSide,
            LoyaltyEarnSource.Referral,
            orderId,
            SystemActor,
            requestId: null,
            cancellationToken);

        await loyaltyService.GrantPointsManuallyAsync(
            referral.ReferredUserId,
            ReferralPolicy.PointsPerSide,
            LoyaltyEarnSource.Referral,
            orderId,
            SystemActor,
            requestId: null,
            cancellationToken);

        referral.MarkQualified(
            firstQualifyingOrderId: orderId,
            pointsToReferrer: ReferralPolicy.PointsPerSide,
            pointsToReferred: ReferralPolicy.PointsPerSide,
            actorId: SystemActor);

        // Bump the inviter's "X friends qualified" counter so the Rewards
        // tab stat updates without a recount.
        var referralCode = await referralCodeRepository.GetByIdAsync(
            referral.ReferralCodeId, cancellationToken);
        referralCode?.RecordUse(SystemActor);
    }

    public async Task ExpireStaleReferralsAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-ReferralPolicy.QualifyingWindowDays);
        var expirable = await referralRepository.GetExpirableAsync(cutoff, cancellationToken);
        foreach (var referral in expirable)
        {
            referral.MarkExpired(SystemActor);
        }

        if (expirable.Count > 0)
        {
            logger.LogInformation(
                "Expired {Count} stale referrals past the {Days}-day qualifying window.",
                expirable.Count, ReferralPolicy.QualifyingWindowDays);
        }
    }

    /// <summary>
    /// Cryptographically-strong random code from <see cref="CodeAlphabet"/>.
    /// Uses <see cref="RandomNumberGenerator"/> rather than <c>Random</c> so
    /// codes can't be predicted from a known seed (defence in depth — codes
    /// aren't secret tokens but reproducible randomness invites abuse).
    /// </summary>
    private static string GenerateRandomCode(int length)
    {
        var chars = new char[length];
        Span<byte> bytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(bytes);
        for (int i = 0; i < length; i++)
        {
            // Modulo a 28-char alphabet from a 256-value byte introduces a
            // tiny bias (256 % 28 = 4). For a 6-char shareable code this is
            // immaterial — full unbiased rejection sampling would be overkill.
            chars[i] = CodeAlphabet[bytes[i] % CodeAlphabet.Length];
        }
        return new string(chars);
    }
}
