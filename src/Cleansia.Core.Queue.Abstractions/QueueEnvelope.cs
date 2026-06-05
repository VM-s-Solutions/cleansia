namespace Cleansia.Core.Queue.Abstractions;

/// <summary>
/// ADR-0002 D2.1 — the frozen wire wrapper for every dispatched message. Gives the consumer a stable,
/// deterministic idempotency key so a duplicate enqueue (the Stripe-retry hazard) and a redelivery
/// collapse onto the same key and the effect is recognized as already-done.
///
/// <para><see cref="MessageKey"/> is <b>deterministic, set by the producer</b> (NEVER a fresh
/// <c>Guid</c> per send) — see <see cref="MessageKeys"/> for the frozen formulas.</para>
///
/// <para><see cref="TenantId"/> is carried explicitly because the queue consumer has no JWT — it sets
/// the tenant override before reading tenant-scoped rows. Nullable: <c>null</c> in single-tenant mode.
/// Redundant-but-harmless for <c>notifications-dispatch</c> (the push payload already carries it).</para>
/// </summary>
public sealed record QueueEnvelope<T>(
    string MessageKey,
    string? TenantId,
    T Payload);
