using Cleansia.Core.Domain.Payments;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Lookup + insert for <see cref="ProcessedStripeEvent"/>. Used exclusively
/// by <see cref="Features.Payments.HandlePaymentNotification"/> to enforce
/// webhook idempotency.
/// </summary>
public interface IProcessedStripeEventRepository : IRepository<ProcessedStripeEvent, string>
{
    /// <summary>
    /// True iff we've already committed a row for this Stripe event id.
    /// The caller (webhook handler) short-circuits when this returns true.
    /// </summary>
    Task<bool> HasProcessedAsync(string stripeEventId, CancellationToken cancellationToken);
}
