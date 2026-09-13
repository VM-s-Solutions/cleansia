using Cleansia.Core.Domain.Memberships;

namespace Cleansia.Core.Domain.Repositories;

public interface IMembershipPlanPriceRepository : IRepository<MembershipPlanPrice, string>
{
    /// <summary>Rows for these plans in ONE currency, keyed by plan id. An absent key is a plan not on sale in that currency.</summary>
    Task<Dictionary<string, MembershipPlanPrice>> GetForPlansAsync(
        IReadOnlyCollection<string> planIds, string currencyId, CancellationToken cancellationToken);

    Task<MembershipPlanPrice?> GetForPlanAsync(string planId, string currencyId, CancellationToken cancellationToken);

    /// <summary>Every currency's row for one plan, with <see cref="MembershipPlanPrice.Currency"/> loaded — the admin detail.</summary>
    Task<IReadOnlyList<MembershipPlanPrice>> GetAllForPlanAsync(string planId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether any row OTHER than <paramref name="exceptPlanId"/>'s row in <paramref name="exceptCurrencyCode"/>
    /// already carries this Stripe Price — another plan's row, or the same plan's row in another currency.
    /// A null <paramref name="exceptPlanId"/> excepts nothing (create).
    /// </summary>
    Task<bool> IsStripePriceIdUsedAsync(string stripePriceId, string? exceptPlanId, string? exceptCurrencyCode, CancellationToken cancellationToken);
}
