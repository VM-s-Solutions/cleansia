using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Messaging;

/// <summary>
/// Durable claim row for the queue-consumer idempotency guard (the durable backing for
/// <see cref="Queue.Abstractions.IIdempotencyGuard"/>). One row per deterministic consumer message key:
/// the guard inserts a row to CLAIM the key before the terminal effect, so a redelivery / parallel
/// retry / worker-restart sees the existing row and short-circuits (at-most-once-after-the-marker).
/// Mirrors <see cref="Payments.ProcessedStripeEvent"/>, the established at-most-once idempotency-row
/// pattern.
///
/// <para><b>Tenant-global by design — intentionally NOT <see cref="ITenantEntity"/> (a reasoned S8
/// exception).</b> The queue consumer CLAIMS the key BEFORE it sets any tenant override (the override is
/// derived from the message payload, which is read after the claim), so there is no tenant in context at
/// claim time. <see cref="MessageKey"/> already embeds globally-unique ULIDs (e.g. an order id), so a
/// composite <c>(TenantId, MessageKey)</c> key would only WEAKEN dedup for the null-tenant rows the
/// claim writes. The dedup must work across every tenant the platform serves.</para>
///
/// <para>Insert pattern: a UNIQUE index on <see cref="MessageKey"/> is the load-bearing constraint. Two
/// parallel redeliveries can both miss the existence check and both attempt the insert — exactly one
/// commit succeeds; the other gets a Postgres 23505 (DbUpdateException) which the guard converts into an
/// "already-claimed" → ack.</para>
/// </summary>
public class ProcessedMessage : BaseEntity
{
    /// <summary>
    /// The deterministic, self-describing consumer message key (the prefix — <c>push:</c> / <c>email:</c>
    /// — encodes the effect; the suffix embeds the globally-unique subject ids). The unique index makes
    /// it globally unique in our DB. Capped at 256 chars to cover the longest composite key comfortably.
    /// No separate event-type audit column is kept: unlike Stripe's opaque <c>evt_…</c> id, this key is
    /// self-describing, so the row stays minimal.
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string MessageKey { get; private set; } = default!;

    /// <summary>Server time (UTC) the claim row was committed (i.e. when the key was claimed).</summary>
    public DateTime ProcessedAt { get; private set; }

    public static ProcessedMessage Create(string messageKey)
        => new()
        {
            MessageKey = messageKey,
            ProcessedAt = DateTime.UtcNow,
        };
}
