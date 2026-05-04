using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Loyalty;

/// <summary>
/// Returns all tier configs for the current tenant — used by the Rewards
/// tab "tier ladder" view that shows every tier (locked / unlocked /
/// current) with thresholds + perks.
/// </summary>
public class GetLoyaltyTiers
{
    public record Query() : IQuery<Response>;

    public record Response(IEnumerable<TierInfo> Tiers);

    public record TierInfo(
        LoyaltyTier Tier,
        int LifetimePointsThreshold,
        decimal DiscountPercent,
        decimal? MinimumOrderAmountForDiscount,
        IEnumerable<TierPerk> Perks);

    public record TierPerk(string Icon, string LabelKey);

    public class Handler(ILoyaltyTierConfigRepository loyaltyTierConfigRepository)
        : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var configs = await loyaltyTierConfigRepository.GetAllForTenantAsync(cancellationToken);

            var tiers = configs
                .OrderBy(c => c.LifetimePointsThreshold)
                .Select(c => new TierInfo(
                    Tier: c.Tier,
                    LifetimePointsThreshold: c.LifetimePointsThreshold,
                    DiscountPercent: c.DiscountPercent,
                    MinimumOrderAmountForDiscount: c.MinimumOrderAmountForDiscount,
                    Perks: ParsePerks(c.PerksJson)))
                .ToList();

            return BusinessResult.Success(new Response(tiers));
        }

        private static IEnumerable<TierPerk> ParsePerks(string? perksJson)
        {
            // Reuse the same parser shape as GetMyLoyalty to stay consistent.
            return GetMyLoyalty.Handler
                .ParsePerks(perksJson)
                .Select(p => new TierPerk(p.Icon, p.LabelKey))
                .ToList();
        }
    }
}
