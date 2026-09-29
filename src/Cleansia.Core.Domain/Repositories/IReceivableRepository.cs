using Cleansia.Core.Domain.Payments;

namespace Cleansia.Core.Domain.Repositories;

public interface IReceivableRepository : IRepository<Receivable, string>
{
    /// <summary>Whether the customer owes an open receivable to any operating company.</summary>
    Task<bool> HasOpenForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>What the customer owes any operating company and has not settled, with its order and currency, oldest first.</summary>
    Task<IReadOnlyList<Receivable>> GetOpenForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// A receivable of any company with its order and currency: the Stripe webhook and the customer's pay
    /// link carry no company of their own.
    /// </summary>
    Task<Receivable?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// The off-session charge sweep's batch: open receivables of every company never yet charged, with their
    /// order and currency, oldest first.
    /// </summary>
    Task<IReadOnlyList<Receivable>> GetUnchargedOpenIgnoringTenantAsync(int take, CancellationToken cancellationToken);
}
