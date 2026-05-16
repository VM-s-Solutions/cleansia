using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Loyalty.Admin.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Loyalty.Admin;

/// <summary>
/// Returns all four tier configs for the current tenant in threshold-asc
/// order. Mirrors <see cref="Loyalty.GetLoyaltyTiers"/> but exposes the
/// editable shape (id, raw perks JSON, audit timestamps) instead of the
/// rendered customer-facing perk list.
/// </summary>
public class GetAllTierConfigs
{
    public record Query() : IQuery<Response>;

    public record Response(IReadOnlyList<TierConfigAdminDto> Tiers);

    public class Handler(ILoyaltyTierConfigRepository tierConfigRepository)
        : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var configs = await tierConfigRepository.GetAllForTenantAsync(cancellationToken);

            var tiers = configs
                .OrderBy(c => c.LifetimePointsThreshold)
                .Select(c => new TierConfigAdminDto(
                    Id: c.Id,
                    Tier: c.Tier,
                    LifetimePointsThreshold: c.LifetimePointsThreshold,
                    DiscountPercent: c.DiscountPercent,
                    MinimumOrderAmountForDiscount: c.MinimumOrderAmountForDiscount,
                    PerksJson: c.PerksJson,
                    CreatedOn: c.CreatedOn,
                    UpdatedOn: c.UpdatedOn))
                .ToList();

            return BusinessResult.Success(new Response(tiers));
        }
    }
}
