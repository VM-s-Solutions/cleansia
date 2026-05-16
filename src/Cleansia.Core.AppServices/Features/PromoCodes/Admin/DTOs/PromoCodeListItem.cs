using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.AppServices.Features.PromoCodes.Admin.DTOs;

/// <summary>
/// Row shape for the admin promo-codes table. Mirrors the persisted shape
/// plus the joined currency code for fixed-discount rendering.
/// </summary>
public record PromoCodeListItem(
    string Id,
    string Code,
    PromoCodeType Type,
    decimal? DiscountPercent,
    decimal? DiscountAmount,
    string? CurrencyId,
    string? CurrencyCode,
    decimal? MinimumOrderAmount,
    int MaxRedemptionsPerUser,
    int? GlobalMaxRedemptions,
    int CurrentRedemptionsCount,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    bool IsActive,
    string? Description,
    DateTimeOffset CreatedOn);
