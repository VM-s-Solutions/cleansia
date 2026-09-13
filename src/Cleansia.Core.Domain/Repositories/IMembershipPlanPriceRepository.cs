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

    /// <summary>Whether a row on any OTHER plan than <paramref name="exceptPlanId"/> already carries this Stripe Price.</summary>
    Task<bool> IsStripePriceIdUsedAsync(string stripePriceId, string? exceptPlanId, CancellationToken cancellationToken);
}
