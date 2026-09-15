namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Discrete failure reasons returned by <see cref="IPromoCodeService"/>. The
/// stringified enum value is propagated to the client so the frontend can map
/// it to its own i18n bundle (no server-side translation).
/// </summary>
public enum PromoCodeError
{
    NotFound,
    Inactive,
    Expired,
    NotYetValid,
    GlobalLimitReached,
    PerUserLimitReached,
    BelowMinimumOrderAmount,
    CurrencyMismatch,
}

/// <summary>
/// Result of <see cref="IPromoCodeService.PreviewAsync"/>. <see cref="PromoCodeId"/>
/// is non-null on success so the caller can persist it on the order.
/// </summary>
public record PromoCodePreviewResult(
    bool Success,
    decimal DiscountAmount,
    string? PromoCodeId,
    PromoCodeError? Error);

/// <summary>
/// Result of <see cref="IPromoCodeService.ApplyAsync"/>. Idempotent — re-calling
/// for the same order returns the originally-applied amount.
/// </summary>
public record PromoCodeApplyResult(
    bool Success,
    decimal AppliedDiscount,
    string? PromoCodeId,
    PromoCodeError? Error);

public interface IPromoCodeService
{
    /// <summary>
    /// Validate a code + compute the discount it would yield without writing
    /// any state. Used by the customer-facing Validate endpoint, by the
    /// CreateOrder validator (which refuses the booking on any error) and by
    /// the CreateOrder handler (which then calls <see cref="ApplyAsync"/>
    /// after the order id is known).
    /// </summary>
    /// <param name="orderCurrencyId">
    /// Currency the order will be billed in — the service address's country's.
    /// A code bound to a currency applies only when this one matches it, so
    /// every caller, advisory or not, must pass the currency the order will
    /// actually be priced in: an advisory preview against a different currency
    /// answers a question the checkout never asks.
    /// </param>
    Task<PromoCodePreviewResult> PreviewAsync(
        string code,
        string userId,
        decimal orderSubtotal,
        string? orderCurrencyId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Re-validate a code and, on success, append the
    /// <c>PromoCodeRedemption</c> ledger row + bump the
    /// <c>CurrentRedemptionsCount</c> counter. Idempotent on
    /// <paramref name="orderId"/> — if a redemption row already exists for
    /// this order, returns the originally-applied amount without writing.
    /// </summary>
    Task<PromoCodeApplyResult> ApplyAsync(
        string code,
        string userId,
        string orderId,
        decimal orderSubtotal,
        string? orderCurrencyId,
        CancellationToken cancellationToken);
}
