using Cleansia.Core.Domain.Payments;

namespace Cleansia.Core.Domain.Repositories;

public interface IReceivableRepository : IRepository<Receivable, string>
{
    /// <summary>Whether the customer owes an open receivable to any operating company.</summary>
    Task<bool> HasOpenForUserAsync(string userId, CancellationToken cancellationToken);
}
