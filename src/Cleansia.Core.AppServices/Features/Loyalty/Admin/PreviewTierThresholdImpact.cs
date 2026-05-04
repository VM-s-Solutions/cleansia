using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Loyalty.Admin;

/// <summary>
/// "If I save these new thresholds, how many users move tier?" preview the
/// admin UI calls before committing a tier-config update. Streams every
/// loyalty account's lifetime points into memory once (cheap — int + ulid)
/// and computes current-tier vs proposed-tier with a switch expression
/// rather than two GROUP BYs (Postgres can't easily express the dynamic
/// proposed thresholds without a CTE that EF would generate poorly).
/// </summary>
public class PreviewTierThresholdImpact
{
    public record Command(
        int BronzeThreshold,
        int SilverThreshold,
        int GoldThreshold,
        int PlatinumThreshold) : ICommand<Response>;

    public record Response(IReadOnlyList<TierImpact> Impacts);

    public record TierImpact(LoyaltyTier Tier, int CurrentCount, int NewCount, int Delta);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.BronzeThreshold).GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);
            RuleFor(x => x.SilverThreshold).GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);
            RuleFor(x => x.GoldThreshold).GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);
            RuleFor(x => x.PlatinumThreshold).GreaterThanOrEqualTo(0)
                .WithMessage(BusinessErrorMessage.MustBePositive);
        }
    }

    public class Handler(
        ILoyaltyAccountRepository loyaltyAccountRepository,
        ILoyaltyTierConfigRepository tierConfigRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Current thresholds — null-safe even if a tier row is missing.
            var configs = await tierConfigRepository.GetAllForTenantAsync(cancellationToken);
            var current = new Thresholds(
                Bronze: configs.FirstOrDefault(c => c.Tier == LoyaltyTier.BronzeCleaner)?.LifetimePointsThreshold ?? 0,
                Silver: configs.FirstOrDefault(c => c.Tier == LoyaltyTier.SilverMopper)?.LifetimePointsThreshold ?? int.MaxValue,
                Gold: configs.FirstOrDefault(c => c.Tier == LoyaltyTier.GoldPolisher)?.LifetimePointsThreshold ?? int.MaxValue,
                Platinum: configs.FirstOrDefault(c => c.Tier == LoyaltyTier.PlatinumSparkler)?.LifetimePointsThreshold ?? int.MaxValue);

            var proposed = new Thresholds(
                Bronze: command.BronzeThreshold,
                Silver: command.SilverThreshold,
                Gold: command.GoldThreshold,
                Platinum: command.PlatinumThreshold);

            // Pull just (id, lifetimePoints) — no tracking, no includes.
            var rows = await loyaltyAccountRepository.GetQueryable()
                .AsNoTracking()
                .Select(a => a.LifetimePoints)
                .ToListAsync(cancellationToken);

            var currentCounts = new Dictionary<LoyaltyTier, int>
            {
                [LoyaltyTier.BronzeCleaner] = 0,
                [LoyaltyTier.SilverMopper] = 0,
                [LoyaltyTier.GoldPolisher] = 0,
                [LoyaltyTier.PlatinumSparkler] = 0,
            };
            var newCounts = new Dictionary<LoyaltyTier, int>(currentCounts);

            foreach (var points in rows)
            {
                currentCounts[ResolveTier(points, current)] += 1;
                newCounts[ResolveTier(points, proposed)] += 1;
            }

            var impacts = new[]
            {
                LoyaltyTier.BronzeCleaner,
                LoyaltyTier.SilverMopper,
                LoyaltyTier.GoldPolisher,
                LoyaltyTier.PlatinumSparkler,
            }.Select(t => new TierImpact(
                Tier: t,
                CurrentCount: currentCounts[t],
                NewCount: newCounts[t],
                Delta: newCounts[t] - currentCounts[t])).ToList();

            return BusinessResult.Success(new Response(impacts));
        }

        private static LoyaltyTier ResolveTier(int points, Thresholds t)
        {
            if (points >= t.Platinum) return LoyaltyTier.PlatinumSparkler;
            if (points >= t.Gold) return LoyaltyTier.GoldPolisher;
            if (points >= t.Silver) return LoyaltyTier.SilverMopper;
            return LoyaltyTier.BronzeCleaner;
        }

        private record Thresholds(int Bronze, int Silver, int Gold, int Platinum);
    }
}
