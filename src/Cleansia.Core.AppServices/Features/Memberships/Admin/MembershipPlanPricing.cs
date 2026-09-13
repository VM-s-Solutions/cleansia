using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Memberships.Admin;

/// <summary>
/// The per-currency price rules and the upsert the two admin plan commands share, so create and
/// update cannot drift apart on what a price entry must look like or how a row is written.
/// </summary>
public static class MembershipPlanPricing
{
    /// <summary>
    /// Every key names a currency the platform knows. Null or empty passes: a plan may be unpriced
    /// everywhere (ADR-0059 D4) — that is "Plus is not on sale", not an invalid plan.
    /// </summary>
    public static IRuleBuilderOptions<T, Dictionary<string, MembershipPlanPriceInput>?> MustBeKeyedByKnownCurrencyCodes<T>(
        this IRuleBuilder<T, Dictionary<string, MembershipPlanPriceInput>?> ruleBuilder,
        ICurrencyRepository currencyRepository)
    {
        return ruleBuilder
            .MustAsync(async (prices, cancellationToken) =>
            {
                if (prices is null || prices.Count == 0)
                {
                    return true;
                }

                var known = await currencyRepository.GetAll()
                    .Select(c => c.Code)
                    .ToListAsync(cancellationToken);
                return prices.Keys.All(known.Contains);
            })
            .WithMessage(BusinessErrorMessage.CurrencyNotFound);
    }

    /// <summary>
    /// One row per currency the form sent, created or updated; currencies the payload does not mention
    /// keep whatever row they have (the package idiom). An unknown code is skipped — the validator has
    /// already refused it.
    /// </summary>
    public static async Task UpsertAsync(
        string planId,
        Dictionary<string, MembershipPlanPriceInput>? prices,
        IMembershipPlanPriceRepository membershipPlanPriceRepository,
        ICurrencyRepository currencyRepository,
        CancellationToken cancellationToken)
    {
        if (prices is null || prices.Count == 0)
        {
            return;
        }

        var byCode = await currencyRepository.GetAll()
            .ToDictionaryAsync(c => c.Code, c => c.Id, cancellationToken);

        foreach (var (code, entry) in prices)
        {
            if (!byCode.TryGetValue(code, out var currencyId))
            {
                continue;
            }

            var existing = await membershipPlanPriceRepository.GetForPlanAsync(planId, currencyId, cancellationToken);
            if (existing is null)
            {
                membershipPlanPriceRepository.Add(MembershipPlanPrice.Create(planId, currencyId, entry.Price, entry.StripePriceId));
            }
            else
            {
                existing.Update(entry.Price, entry.StripePriceId);
            }
        }
    }
}
