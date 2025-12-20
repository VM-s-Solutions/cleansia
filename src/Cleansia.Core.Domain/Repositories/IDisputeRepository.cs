using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Repositories;

public interface IDisputeRepository : IRepository<Dispute, string>
{
    /// <summary>
    /// Gets disputes for a specific order.
    /// </summary>
    IQueryable<Dispute> GetDisputesByOrderId(string orderId);

    /// <summary>
    /// Gets disputes created by a specific user.
    /// </summary>
    IQueryable<Dispute> GetDisputesByUserId(string userId);

    /// <summary>
    /// Gets disputes by status.
    /// </summary>
    IQueryable<Dispute> GetDisputesByStatus(DisputeStatus status);

    /// <summary>
    /// Gets all disputes with order and user information.
    /// Used for admin dashboard and listing.
    /// </summary>
    IQueryable<Dispute> GetDisputesWithDetails();

    /// <summary>
    /// Gets a dispute by ID with all related data (messages, evidence).
    /// </summary>
    Task<Dispute?> GetDisputeWithDetailsAsync(string disputeId);
}
