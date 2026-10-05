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
    /// Sum of an order's succeeded refund amounts: the card money confirmed back, which decides the
    /// payment status, the loyalty clawback and payroll's collected fee. A ceiling on what may still go
    /// back adds <see cref="GetPendingRefundTotalForOrderAsync"/>, because a pending row may be one
    /// Stripe already paid.
    /// </summary>
    Task<decimal> GetSucceededRefundTotalForOrderAsync(string orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Sum of an order's pending refund amounts, leaving out the row keyed <paramref name="exceptRefundKey"/>:
    /// the refund being re-driven is not owed on top of itself.
    /// </summary>
    Task<decimal> GetPendingRefundTotalForOrderAsync(
        string orderId, string? exceptRefundKey, CancellationToken cancellationToken);

    /// <summary>
    /// The batch form of <see cref="GetSucceededRefundTotalForOrderAsync"/>: Σ succeeded refund amounts
    /// per order, any date, keyed by order id; an order with none is absent. The card leg only — the
    /// credit share of a refund is a <c>CreditTransaction</c>, read by
    /// <c>ICreditAccountRepository.GetReturnedTotalsByOrderAsync</c>.
    /// </summary>
    Task<IReadOnlyDictionary<string, decimal>> GetSucceededRefundTotalsByOrderAsync(
        IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken);
}
