using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.DeadLettering;

/// <summary>
/// ADR-0002 D3 (F3) — a durable, admin-visible record of a poisoned queue message. Written by the
/// per-queue <c>-poison</c> consumers (the tactical Wave-0 poison floor): when a business-queue
/// message exhausts <c>maxDequeueCount</c> (host.json = 5) the Storage-queue runtime moves it to
/// <c>&lt;queue&gt;-poison</c>; the paired poison consumer persists THIS row + logs at Error (the
/// Sentry/AppInsights alert) + acks, so the (especially fiscal/financial) work is recoverable/replayable
/// instead of silently lost.
///
/// <para><b>Tenant-nullable by design.</b> Implements <see cref="ITenantEntity"/> so the global query
/// filter scopes admin reads when a tenant IS known, but a poisoned body may be unparseable/malformed,
/// so <see cref="ITenantEntity.TenantId"/> can be <c>null</c> (unknown tenant). The poison consumer
/// records the raw body verbatim and does NOT attempt to derive a tenant from it.</para>
///
/// <para>The poison consumer NEVER re-processes the original effect — this row IS the recovery source,
/// not a retry.</para>
/// </summary>
public class DeadLetter : Auditable, ITenantEntity
{
    /// <summary>
    /// The business queue the message poisoned on (the logical queue name WITHOUT the <c>-poison</c>
    /// suffix), e.g. <c>generate-receipt</c>. One of <see cref="Queue.Abstractions.QueueNames"/>.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string SourceQueue { get; private set; } = default!;

    /// <summary>
    /// The raw, verbatim message body as it arrived on the <c>-poison</c> queue (the serialized
    /// <c>QueueEnvelope&lt;T&gt;</c> or a bare/malformed payload). Stored as <c>text</c> (unbounded) so
    /// nothing is truncated — this is the load-bearing recovery/replay source for fiscal artifacts.
    /// </summary>
    [Required]
    public string RawBody { get; private set; } = default!;

    /// <summary>
    /// Optional error / exception text captured when the row was written (when the poison consumer has
    /// it). <c>null</c> when the Storage-queue runtime poisoned the message and no exception is in hand.
    /// Stored as <c>text</c> (unbounded) so a full stack trace fits.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>Server time (UTC) the dead-letter row was recorded.</summary>
    public DateTime DeadLetteredAt { get; private set; }

    public static DeadLetter Create(string sourceQueue, string rawBody, string? error = null)
        => new()
        {
            SourceQueue = sourceQueue,
            RawBody = rawBody,
            Error = error,
            DeadLetteredAt = DateTime.UtcNow,
        };
}
