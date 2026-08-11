using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// Coordinates promo-code validation and redemption. Mirrors the
/// <see cref="LoyaltyService"/> shape — handlers call into this rather than
/// poking the repos directly.
/// </summary>
public sealed class PromoCodeService(
    IPromoCodeRepository promoCodeRepository,
    IPromoCodeRedemptionRepository redemptionRepository,
    ILogger<PromoCodeService> logger) : IPromoCodeService
{
    public async Task<PromoCodePreviewResult> PreviewAsync(
        string code,
        string userId,
        decimal orderSubtotal,
        string? orderCurrencyId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new PromoCodePreviewResult(false, 0m, null, PromoCodeError.NotFound);
        }

        var normalised = code.Trim().ToUpperInvariant();
        var promoCode = await promoCodeRepository.GetByCodeAsync(normalised, cancellationToken);
        if (promoCode == null)
        {
            return new PromoCodePreviewResult(false, 0m, null, PromoCodeError.NotFound);
        }

        var validationError = ValidateAvailability(promoCode, DateTimeOffset.UtcNow);
        if (validationError != null)
        {
            return new PromoCodePreviewResult(false, 0m, null, validationError);
        }

        // Per-user cap. Skip when no user context (anon flow) — the calling
        // handler should already short-circuit there.
        if (!string.IsNullOrEmpty(userId))
        {
            var priorRedemptions = await redemptionRepository.CountForUserAndCodeAsync(
                userId, promoCode.Id, cancellationToken);
            if (priorRedemptions >= promoCode.MaxRedemptionsPerUser)
            {
                return new PromoCodePreviewResult(false, 0m, null, PromoCodeError.PerUserLimitReached);
            }
        }

        if (promoCode.MinimumOrderAmount.HasValue && orderSubtotal < promoCode.MinimumOrderAmount.Value)
        {
            return new PromoCodePreviewResult(false, 0m, null, PromoCodeError.BelowMinimumOrderAmount);
        }

        // Fixed-amount codes only apply when the currencies match. Percent
        // codes are currency-agnostic.
        if (promoCode.Type == PromoCodeType.FixedDiscount
            && !string.IsNullOrEmpty(promoCode.CurrencyId)
            && !string.IsNullOrEmpty(orderCurrencyId)
            && !string.Equals(promoCode.CurrencyId, orderCurrencyId, StringComparison.Ordinal))
        {
            return new PromoCodePreviewResult(false, 0m, null, PromoCodeError.CurrencyMismatch);
        }

        var discount = ComputeDiscount(promoCode, orderSubtotal);
        return new PromoCodePreviewResult(true, discount, promoCode.Id, null);
    }

    public async Task<PromoCodeApplyResult> ApplyAsync(
        string code,
        string userId,
        string orderId,
        decimal orderSubtotal,
        string? orderCurrencyId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(orderId))
        {
            throw new ArgumentException("OrderId is required to apply a promo code.", nameof(orderId));
        }

        // Idempotency — return this order's existing redemption row as-is. It only sees a COMMITTED
        // row: under the interim's change-tracked insert (ADR-0038 §D3) a second call in the same
        // unit of work would miss the staged row and collide on the unique OrderId index at commit.
        // Nothing calls ApplyAsync twice per request today — CreateOrder is its only caller.
        var existing = await redemptionRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (existing != null)
        {
            return new PromoCodeApplyResult(true, existing.AppliedDiscount, existing.PromoCodeId, null);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return new PromoCodeApplyResult(false, 0m, null, PromoCodeError.NotFound);
        }

        var normalised = code.Trim().ToUpperInvariant();
        var promoCode = await promoCodeRepository.GetByCodeAsync(normalised, cancellationToken);
        if (promoCode == null)
        {
            return new PromoCodeApplyResult(false, 0m, null, PromoCodeError.NotFound);
        }

        var validationError = ValidateAvailability(promoCode, DateTimeOffset.UtcNow);
        if (validationError != null)
        {
            return new PromoCodeApplyResult(false, 0m, null, validationError);
        }

        if (string.IsNullOrEmpty(userId))
        {
            // Apply requires a user — codes are tied to authenticated customers.
            return new PromoCodeApplyResult(false, 0m, null, PromoCodeError.NotFound);
        }

        // Cheap in-memory FAST PATH for the per-user cap (the common case). This is NOT the source
        // of truth — it just avoids a DB write when the cap is plainly already used. The atomic
        // reservation below is the arbiter and closes the check-then-act race (S7).
        var priorRedemptions = await redemptionRepository.CountForUserAndCodeAsync(
            userId, promoCode.Id, cancellationToken);
        if (priorRedemptions >= promoCode.MaxRedemptionsPerUser)
        {
            return new PromoCodeApplyResult(false, 0m, null, PromoCodeError.PerUserLimitReached);
        }

        if (promoCode.MinimumOrderAmount.HasValue && orderSubtotal < promoCode.MinimumOrderAmount.Value)
        {
            return new PromoCodeApplyResult(false, 0m, null, PromoCodeError.BelowMinimumOrderAmount);
        }

        if (promoCode.Type == PromoCodeType.FixedDiscount
            && !string.IsNullOrEmpty(promoCode.CurrencyId)
            && !string.IsNullOrEmpty(orderCurrencyId)
            && !string.Equals(promoCode.CurrencyId, orderCurrencyId, StringComparison.Ordinal))
        {
            return new PromoCodeApplyResult(false, 0m, null, PromoCodeError.CurrencyMismatch);
        }

        var discount = ComputeDiscount(promoCode, orderSubtotal);

        // GLOBAL CAP — atomic conditional increment. Replaces the old read-then-IncrementRedemptions
        // race: a single SQL UPDATE bumps CurrentRedemptionsCount only while it is still under the
        // cap. 0 rows affected ⇒ the cap is reached ⇒ GlobalLimitReached, and NO redemption row is
        // reserved. NOTE: this repository call issues SQL immediately and bypasses the UnitOfWork
        // pipeline (see TryIncrementGlobalRedemptionsAsync) — a knowing, required exception to the
        // "never commit outside the pipeline" rule. It does not roll back the order.
        var globalSlotReserved = await promoCodeRepository.TryIncrementGlobalRedemptionsAsync(
            promoCode.Id, cancellationToken);
        if (!globalSlotReserved)
        {
            return new PromoCodeApplyResult(false, 0m, null, PromoCodeError.GlobalLimitReached);
        }

        // PER-USER CAP — reserve the next free 0-based SlotOrdinal and stage the redemption row.
        // Returns the row on success, or null when no slot is available (cap reached) ⇒
        // PerUserLimitReached, a clean RESULT rather than an unhandled DbUpdateException.
        //
        // The finally releases the global slot on ANY non-success — the null return AND a throw.
        // The increment above auto-commits, so a reservation that throws (transient DB error,
        // timeout) would otherwise burn a global slot permanently: a 100-redemption campaign dies
        // after 100 failed bookings. The reservation's own failure always propagates unchanged
        // (ADR-0038 §D6, §D8).
        PromoCodeRedemption? redemption = null;
        try
        {
            redemption = await redemptionRepository.TryReserveRedemptionSlotAsync(
                userId, promoCode.Id, promoCode.MaxRedemptionsPerUser, orderId, discount, cancellationToken);
        }
        finally
        {
            if (redemption == null)
            {
                await ReleaseGlobalSlotAsync(promoCode, userId, orderId);
            }
        }

        if (redemption == null)
        {
            return new PromoCodeApplyResult(false, 0m, null, PromoCodeError.PerUserLimitReached);
        }

        logger.LogInformation(
            "Promo code {Code} redeemed by user {UserId} on order {OrderId} for {Discount} (slot {Slot}).",
            promoCode.Code, userId, orderId, discount, redemption.SlotOrdinal);

        return new PromoCodeApplyResult(true, discount, promoCode.Id, null);
    }

    private async Task ReleaseGlobalSlotAsync(PromoCode promoCode, string userId, string orderId)
    {
        try
        {
            // Not the caller's token: the increment is already durable and outlives an aborted
            // request, so a cancelled token must not skip the release.
            await promoCodeRepository.DecrementGlobalRedemptionsAsync(promoCode.Id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // An exception escaping here would REPLACE the reservation's in the finally above — and
            // both failing is one transient error on one DbContext, exactly the case this
            // compensation exists for. Catching the release alone restores the invariant without
            // hiding the caller's failure (ADR-0038 §D6). The slot stays burned until someone
            // reconciles it, so this is Error, not Warning.
            logger.LogError(
                ex,
                "Promo global-slot release failed for code {Code} ({PromoCodeId}), user {UserId}, order {OrderId}. " +
                "CurrentRedemptionsCount is now one higher than the redemption ledger and must be reconciled " +
                "by hand (ADR-0038 §D6.4).",
                promoCode.Code, promoCode.Id, userId, orderId);
        }
    }

    private static PromoCodeError? ValidateAvailability(PromoCode promoCode, DateTimeOffset now)
    {
        if (!promoCode.IsActive)
        {
            return PromoCodeError.Inactive;
        }
        if (promoCode.ValidFrom.HasValue && now < promoCode.ValidFrom.Value)
        {
            return PromoCodeError.NotYetValid;
        }
        if (promoCode.ValidUntil.HasValue && now > promoCode.ValidUntil.Value)
        {
            return PromoCodeError.Expired;
        }
        if (promoCode.GlobalMaxRedemptions.HasValue
            && promoCode.CurrentRedemptionsCount >= promoCode.GlobalMaxRedemptions.Value)
        {
            return PromoCodeError.GlobalLimitReached;
        }
        return null;
    }

    private static decimal ComputeDiscount(PromoCode promoCode, decimal orderSubtotal)
    {
        return promoCode.Type switch
        {
            PromoCodeType.PercentDiscount => promoCode.DiscountPercent.HasValue
                ? Math.Round(orderSubtotal * promoCode.DiscountPercent.Value, 2, MidpointRounding.AwayFromZero)
                : 0m,
            PromoCodeType.FixedDiscount => promoCode.DiscountAmount.HasValue
                // Don't refund more than the order is worth.
                ? Math.Min(promoCode.DiscountAmount.Value, orderSubtotal)
                : 0m,
            _ => 0m,
        };
    }
}
