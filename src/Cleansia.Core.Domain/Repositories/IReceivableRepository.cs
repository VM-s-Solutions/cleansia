using Cleansia.Core.Domain.Payments;

namespace Cleansia.Core.Domain.Repositories;

public interface IReceivableRepository : IRepository<Receivable, string>
{
    /// <summary>Whether the customer owes an open receivable to any operating company.</summary>
    Task<bool> HasOpenForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>What the customer owes any operating company and has not settled, with its order and currency, oldest first.</summary>
    Task<IReadOnlyList<Receivable>> GetOpenForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>The price of the order the customer did not pay the cleaner at the door, in any status, or null.</summary>
    Task<Receivable?> GetUnpaidCashForOrderAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// A receivable of any company with its order, the order's crew and its currency: the Stripe webhook and
    /// the customer's pay link carry no company of their own.
    /// </summary>
    Task<Receivable?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    /// The off-session charge sweep's batch: open receivables never yet charged, with their order and
    /// currency, oldest first, of every company but a frozen one, whose books refuse the attempt's write.
    /// </summary>
    Task<IReadOnlyList<Receivable>> GetUnchargedOpenIgnoringTenantAsync(int take, CancellationToken cancellationToken);
}
