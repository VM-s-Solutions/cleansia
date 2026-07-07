namespace Cleansia.Core.Queue.Abstractions;

/// <summary>
/// Dedup for a queue consumer whose terminal effect is non-transactional and has no domain
/// target-state to check (e.g. a sent email or push has no DB row that proves it happened).
/// Two modes (ADR-0023), chosen by how repeatable the effect is:
///
/// <para><b>Mode A — claim-then-act (at-most-once)</b> — <see cref="AlreadyProcessedAsync"/> atomically
/// claims the deterministic <c>messageKey</c> BEFORE the effect. MANDATORY for effects that are not
/// safely repeatable (receipt/invoice generation, pay calculation — anything money-shaped): the
/// residual is a lost effect when a crash lands between the claim and the act, never a duplicate.
/// Example: the push consumer.</para>
///
/// <para><b>Mode B — act-then-claim (at-least-once)</b> — <see cref="HasProcessedAsync"/> (non-claiming
/// check) → effect → <see cref="MarkProcessedAsync"/> (post-success claim). Permitted ONLY where a
/// duplicate effect is benign: a failed effect leaves the key unclaimed so the queue retry genuinely
/// retries; the residual is a rare duplicate, never a lost effect. Example: the send-email
/// consumer.</para>
/// </summary>
public interface IIdempotencyGuard
{
    /// <summary>
    /// Atomically claims <paramref name="messageKey"/>. Returns <c>true</c> when the key was already
    /// claimed (the effect already happened → the caller acks without acting), <c>false</c> when this
    /// call won the claim (the caller proceeds with its terminal effect).
    /// </summary>
    Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default);

    /// <summary>
    /// Non-claiming read: <c>true</c> when <paramref name="messageKey"/> is already claimed. Never
    /// writes — a redelivery filter, deliberately not an atomic control; the act-then-claim caller
    /// uses it to skip an already-completed effect, then acts, then claims via
    /// <see cref="MarkProcessedAsync"/>.
    /// </summary>
    Task<bool> HasProcessedAsync(string messageKey, CancellationToken ct = default);

    /// <summary>
    /// Idempotently claims <paramref name="messageKey"/> AFTER the effect succeeded, in its own
    /// committed unit of work. Double-marking and marking a key a concurrent consumer claimed first are
    /// both safe no-ops (a unique violation is swallowed — the row exists, which is all the caller
    /// needs); genuine infra faults propagate for the caller to classify.
    /// </summary>
    Task MarkProcessedAsync(string messageKey, CancellationToken ct = default);
}
