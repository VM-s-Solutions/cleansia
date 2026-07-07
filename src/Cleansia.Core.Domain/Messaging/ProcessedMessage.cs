using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Messaging;

/// <summary>
/// Durable claim row for the queue-consumer idempotency guard (the durable backing for
/// <see cref="Queue.Abstractions.IIdempotencyGuard"/>). One row per deterministic consumer message key:
/// a redelivery / parallel retry / worker-restart sees the existing row and short-circuits.
/// Mirrors <see cref="Payments.ProcessedStripeEvent"/>, the established idempotency-row pattern.
///
/// <para>What the row MEANS depends on the consumer's claim ordering (the <see cref="MessageKey"/>
/// prefix disambiguates): for claim-then-act consumers (<c>push:</c>) the row means CLAIMED —
/// written before the terminal effect (at-most-once-after-the-marker); for act-then-claim consumers
/// (<c>email:</c>) the row means SENT — written only after the effect succeeded (at-least-once).</para>
///
/// <para><b>Tenant-global by design — intentionally NOT <see cref="ITenantEntity"/> (a reasoned S8
/// exception).</b> Claim-then-act consumers write the row before any tenant override is set;
/// act-then-claim consumers mark it after the send, possibly inside an override — safe either way,
/// because the entity is not tenant-scoped so no ambient tenant can touch the row.
/// <see cref="MessageKey"/> already embeds globally-unique ULIDs (e.g. an order id), so a
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
