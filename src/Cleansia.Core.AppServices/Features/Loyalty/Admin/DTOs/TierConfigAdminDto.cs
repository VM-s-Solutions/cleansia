using Cleansia.Core.Domain.Loyalty;

namespace Cleansia.Core.AppServices.Features.Loyalty.Admin.DTOs;

/// <summary>
/// Full editable shape of a single tier config row. <see cref="Tier"/> is
/// the discriminator and is NOT editable from the admin UI — it identifies
/// which row to update.
/// </summary>
public record TierConfigAdminDto(
    string Id,
    LoyaltyTier Tier,
    int LifetimePointsThreshold,
    decimal DiscountPercent,
    decimal? MinimumOrderAmountForDiscount,
    string PerksJson,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);
