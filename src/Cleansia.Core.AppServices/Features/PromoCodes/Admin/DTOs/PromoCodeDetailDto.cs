using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.AppServices.Features.PromoCodes.Admin.DTOs;

/// <summary>
/// Promo-code detail view including a redemption count for the admin's
/// quick-glance summary.
/// </summary>
public record PromoCodeDetailDto(
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
    int RedemptionCount,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    bool IsActive,
    string? Description,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);
