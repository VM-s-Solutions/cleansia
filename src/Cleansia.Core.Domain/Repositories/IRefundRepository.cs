using Cleansia.Core.Domain.Payments;

namespace Cleansia.Core.Domain.Repositories;

public interface IRefundRepository : IRepository<Refund, string>
{
    /// <summary>
    /// The refund recorded under <paramref name="refundKey"/>, or null. Backs both the pre-Stripe
    /// idempotency fast-path and the post-23505 resolve-to-existing (ADR-0006 D3).
    /// </summary>
    Task<Refund?> GetByRefundKeyAsync(string refundKey, CancellationToken cancellationToken);

    /// <summary>
    /// Sum of an order's succeeded refund amounts — the consumed half of the refundable ceiling
    /// <c>refundable(order) = amountCharged − Σ(succeeded refunds)</c> (ADR-0006 D2). Reads only
    /// <see cref="Cleansia.Core.Domain.Enums.RefundStatus.Succeeded"/> rows so a pending/failed
    /// attempt never shrinks the ceiling.
    /// </summary>
    Task<decimal> GetSucceededRefundTotalForOrderAsync(string orderId, CancellationToken cancellationToken);
}
