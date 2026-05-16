using Cleansia.Core.Domain.Disputes;

namespace Cleansia.Core.Domain.Repositories;

public interface IDisputeRepository : IRepository<Dispute, string>
{
    /// <summary>
    /// All disputes filed by a specific user, with messages + evidence
    /// pre-loaded. Used by the GDPR deletion service to cascade-delete
    /// a user's dispute history.
    /// </summary>
    Task<IReadOnlyList<Dispute>> GetDisputesByUserIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the currently-open dispute (status != Closed) for the given
    /// order, if any. Used by CreateDispute to refuse stacking a second
    /// open dispute on the same order.
    /// </summary>
    Task<Dispute?> GetOpenDisputeForOrderAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a dispute by ID with all related data (messages, evidence).
    /// </summary>
    Task<Dispute?> GetDisputeWithDetailsAsync(string disputeId);
}
