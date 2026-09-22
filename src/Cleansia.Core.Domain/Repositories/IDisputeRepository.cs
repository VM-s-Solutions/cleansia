using System.Linq.Expressions;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Repositories;

public interface IDisputeRepository : IRepository<Dispute, string>
{
    /// <summary>
    /// All disputes filed by a specific user, TRACKED, with messages + evidence pre-loaded. The GDPR
    /// erasure's read: it deletes the evidence blobs, blanks the evidence rows and stamps
    /// <c>TextRetainedUntil</c>; the text itself stays readable until the retention sweep blanks it.
    /// </summary>
    Task<IReadOnlyList<Dispute>> GetDisputesByUserIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the currently-open dispute (status != Closed) for the given
    /// order, if any. Used by CreateDispute to refuse stacking a second
    /// open dispute on the same order.
    /// </summary>
    Task<Dispute?> GetOpenDisputeForOrderAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-only fetch of a dispute by ID with all related data (order, user, messages + authors,
    /// evidence) for the details surface. No-tracking — callers only project to a DTO.
    /// </summary>
    Task<Dispute?> GetDisputeWithDetailsAsync(string disputeId, CancellationToken cancellationToken);

    /// <summary>
    /// Tracked, collection-free fetch for write handlers that mutate one scalar or append one child row
    /// (status change, message append, resolve). The dispute aggregate's mutating methods don't read
    /// its collections, so loading them is pure over-fetch; EF tracks an appended child without
    /// pre-loading the collection. Carries the <c>Order</c> reference nav (read-only single-row join —
    /// no caller mutates it) so the resolve path can put the order's display number on the refund push.
    /// Preserves the exact <c>UserId</c>/<c>TenantId</c> the handlers auth-check against.
    /// </summary>
    Task<Dispute?> GetForUpdateAsync(string disputeId, CancellationToken cancellationToken);

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

    /// <summary>
    /// The caller's own disputes across every operating company — a dispute is stamped with its
    /// ORDER's operator, and a customer who booked across the border filed it under a company that is
    /// not their own. Past the tenant filter, re-pinned by <paramref name="userId"/>, which MUST be the
    /// caller's own id from their JWT (S8; the same shape as <c>IOrderRepository.GetQueryableForOwner</c>).
    /// Staff read their own company's disputes through the filter, never through this.
    /// </summary>
    IQueryable<Dispute> GetQueryableForOwner(string userId);

    Task<Dispute?> GetDisputeWithDetailsForOwnerAsync(string disputeId, string userId, CancellationToken cancellationToken);

    /// <summary>Operator list scope, pinned by the server-resolved company before cross-company identity search.</summary>
    Task<int> GetCountForOperatorAsync(string? operatorTenantId, Expression<Func<Dispute, bool>>? filter, CancellationToken cancellationToken);

    IQueryable<Dispute> GetPagedSortForOperator<TSort>(
        string? operatorTenantId, int offset, int limit, Expression<Func<Dispute, bool>>? filter, IEnumerable<SortDefinition> sort)
        where TSort : BaseSort<Dispute>;

    Task<int> GetCountForOwnerAsync(string userId, Expression<Func<Dispute, bool>>? filter, CancellationToken cancellationToken);

    IQueryable<Dispute> GetPagedSortForOwner<TSort>(
        string userId, int offset, int limit, Expression<Func<Dispute, bool>>? filter, IEnumerable<SortDefinition> sort)
        where TSort : BaseSort<Dispute>;
}
