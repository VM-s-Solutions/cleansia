using System.Text.Json;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Loyalty.Admin;

/// <summary>
/// Admin "look up loyalty account by user id" — the user-explicit mirror of
/// <see cref="Loyalty.GetMyLoyalty"/>. Lazily creates the account on first
/// read so freshly-onboarded users surface with a Bronze 0-point baseline.
/// </summary>
public class GetUserLoyaltyAccount
{
    public record Query(string UserId) : IQuery<Response>;

    public record Response(
        string UserId,
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

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.UserNotFound);
        }
    }

    public class Handler(
        ILoyaltyAccountRepository loyaltyAccountRepository,
        ILoyaltyTierConfigRepository loyaltyTierConfigRepository) : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var account = await loyaltyAccountRepository.EnsureForUserAsync(request.UserId, cancellationToken);

            var allConfigs = await loyaltyTierConfigRepository.GetAllForTenantAsync(cancellationToken);
            var currentConfig = allConfigs.FirstOrDefault(c => c.Tier == account.CurrentTier);
            var nextConfig = allConfigs
                .Where(c => c.LifetimePointsThreshold > account.LifetimePoints)
                .OrderBy(c => c.LifetimePointsThreshold)
                .FirstOrDefault();

            int? pointsToNext = nextConfig != null
                ? Math.Max(0, nextConfig.LifetimePointsThreshold - account.LifetimePoints)
                : null;

            var perks = ParsePerks(currentConfig?.PerksJson);

            return BusinessResult.Success(new Response(
                UserId: request.UserId,
                CurrentTier: account.CurrentTier,
                LifetimePoints: account.LifetimePoints,
                CompletedBookingsCount: account.CompletedBookingsCount,
                TierAchievedOn: account.TierAchievedOn,
                PointsToNextTier: pointsToNext,
                NextTier: nextConfig?.Tier,
                CurrentDiscountPercent: currentConfig?.DiscountPercent ?? 0m,
                CurrentDiscountMinOrderAmount: currentConfig?.MinimumOrderAmountForDiscount,
                CurrentPerks: perks));
        }

        private static IEnumerable<TierPerk> ParsePerks(string? perksJson)
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

        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        private record PerkRow
        {
            public string? Icon { get; init; }
            public string? LabelKey { get; init; }
        }
    }
}
