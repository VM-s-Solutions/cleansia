using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Payments;

/// <summary>
/// Audit row that records a Stripe webhook event we've already processed.
/// <see cref="Features.Payments.HandlePaymentNotification"/> writes one row
/// per delivered event and short-circuits on duplicate <see cref="StripeEventId"/>.
///
/// Tenant-global by design — Stripe webhooks are not tenant-scoped, and
/// the dedupe must work across every tenant the platform serves.
///
/// Insert pattern: the handler relies on a UNIQUE index on
/// <see cref="StripeEventId"/> to enforce dedupe under parallel retries.
/// Both retries do an existence check, both can see no row, both attempt
/// insert — exactly one commit succeeds; the other gets a DbUpdateException
/// and the handler converts that into a "already processed" success path.
/// </summary>
public class ProcessedStripeEvent : BaseEntity
{
    /// <summary>
    /// Stripe-issued event id (<c>evt_...</c>). Globally unique within Stripe;
    /// the unique index in EF makes it globally unique in our DB too. Capped
    /// at 64 chars — Stripe ids are well under that.
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string StripeEventId { get; private set; } = default!;

    /// <summary>
    /// Stripe event type (e.g. <c>checkout.session.completed</c>). Stored for
    /// debugging and audit only — the handler does NOT branch on this field
    /// from the DB. Capped at 128 chars to cover all current + future Stripe
    /// types comfortably.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string EventType { get; private set; } = default!;

    /// <summary>
    /// Timestamp Stripe stamped on the event at creation. Useful for
    /// out-of-order detection in postmortems. UTC, mirrors Stripe.
    /// </summary>
    public DateTime StripeCreatedAt { get; private set; }

    /// <summary>
    /// Server time when we wrote this row (i.e. when we processed the event).
    /// </summary>
    public DateTime ProcessedAt { get; private set; }

    public static ProcessedStripeEvent Create(string stripeEventId, string eventType, DateTime stripeCreatedAt)
        => new()
        {
            StripeEventId = stripeEventId,
            EventType = eventType,
            StripeCreatedAt = stripeCreatedAt,
            ProcessedAt = DateTime.UtcNow,
        };
}
