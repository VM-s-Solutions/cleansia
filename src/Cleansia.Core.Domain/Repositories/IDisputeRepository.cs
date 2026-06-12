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

    /// <summary>
    /// Returns the dispute linked to a given Stripe dispute id, if any. Tenant-scoped — the safe
    /// default for any caller that already has tenant context.
    /// </summary>
    Task<Dispute?> GetByStripeDisputeIdAsync(string stripeDisputeId, CancellationToken cancellationToken);

    /// <summary>
    /// System-level read for the chargeback webhook (ADR-0006 D4): a <c>charge.dispute.updated/closed</c>
    /// event arrives with NO tenant context (the webhook is anonymous), so a tenant-scoped read would
    /// collapse to <c>TenantId == null</c> and miss any non-null-tenant dispute. Bypasses the tenant
    /// query filter; the caller MUST re-scope via <c>SetTenantOverride(dispute.TenantId)</c> before any
    /// mutation so the commit lands under the dispute's tenant.
    /// </summary>
    Task<Dispute?> GetByStripeDisputeIdIgnoringTenantAsync(string stripeDisputeId, CancellationToken cancellationToken);
}
