namespace Cleansia.Core.AppServices.Features.PromoCodes.Admin.DTOs;

/// <summary>
/// Row shape for the per-code redemption history table.
/// </summary>
public record PromoCodeRedemptionListItem(
    string Id,
    string PromoCodeId,
    string UserId,
    string? UserEmail,
    string OrderId,
    decimal AppliedDiscount,
    DateTimeOffset RedeemedOn);
