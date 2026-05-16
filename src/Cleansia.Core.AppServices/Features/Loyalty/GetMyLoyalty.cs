using System.Text.Json;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Loyalty;

public class GetMyLoyalty
{
    public record Query : IQuery<Response>;

    public record Response(
        LoyaltyTier CurrentTier,
        int LifetimePoints,
        int CompletedBookingsCount,
        DateTimeOffset TierAchievedOn,
        int? PointsToNextTier,
        LoyaltyTier? NextTier,
        decimal CurrentDiscountPercent,
        decimal? CurrentDiscountMinOrderAmount,
        IEnumerable<TierPerk> CurrentPerks);

    public record TierPerk(string Icon, string LabelKey);

    public class Handler(
        ILoyaltyAccountRepository loyaltyAccountRepository,
        ILoyaltyTierConfigRepository loyaltyTierConfigRepository,
        IUserSessionProvider userSessionProvider) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var account = await loyaltyAccountRepository.EnsureForUserAsync(userId, cancellationToken);

            var allConfigs = await loyaltyTierConfigRepository.GetAllForTenantAsync(cancellationToken);
            var currentConfig = allConfigs.FirstOrDefault(c => c.Tier == account.CurrentTier);
            var nextConfig = allConfigs
                .Where(c => c.LifetimePointsThreshold > account.LifetimePoints)
                .OrderBy(c => c.LifetimePointsThreshold)
                .FirstOrDefault();

            int? pointsToNextTier = nextConfig != null
                ? Math.Max(0, nextConfig.LifetimePointsThreshold - account.LifetimePoints)
                : null;
            LoyaltyTier? nextTier = nextConfig?.Tier;

            var perks = ParsePerks(currentConfig?.PerksJson);

            var response = new Response(
                CurrentTier: account.CurrentTier,
                LifetimePoints: account.LifetimePoints,
                CompletedBookingsCount: account.CompletedBookingsCount,
                TierAchievedOn: account.TierAchievedOn,
                PointsToNextTier: pointsToNextTier,
                NextTier: nextTier,
                CurrentDiscountPercent: currentConfig?.DiscountPercent ?? 0m,
                CurrentDiscountMinOrderAmount: currentConfig?.MinimumOrderAmountForDiscount,
                CurrentPerks: perks);

            return BusinessResult.Success(response);
        }

        internal static IEnumerable<TierPerk> ParsePerks(string? perksJson)
        {
            if (string.IsNullOrWhiteSpace(perksJson))
            {
                return Array.Empty<TierPerk>();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<PerkRow>>(perksJson, JsonOpts);
                if (parsed == null)
                {
                    return Array.Empty<TierPerk>();
                }
                return parsed
                    .Where(p => !string.IsNullOrEmpty(p.LabelKey))
                    .Select(p => new TierPerk(p.Icon ?? string.Empty, p.LabelKey ?? string.Empty))
                    .ToList();
            }
            catch (JsonException)
            {
                return Array.Empty<TierPerk>();
            }
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private record PerkRow
        {
            public string? Icon { get; init; }
            public string? LabelKey { get; init; }
        }
    }
}
