namespace Cleansia.Core.Queue.Abstractions;

/// <summary>
/// The canonical claim-then-act dedup for a queue consumer whose terminal effect is non-transactional
/// and has no domain target-state to check (e.g. the send-email consumer — an email has no DB row that
/// proves it was sent). Before its terminal effect the consumer calls
/// <see cref="AlreadyProcessedAsync"/>: the first call for a given deterministic
/// <c>messageKey</c> claims it and returns <c>false</c> (proceed); a later call for the same key
/// returns <c>true</c> (already done → ack, do not re-send).
///
/// <para>The guarantee is <b>at-most-once after the claim</b>: a crash between the claim and the send
/// loses that one email — accepted for a non-fiscal notification (a user can re-request).</para>
/// </summary>
public interface IIdempotencyGuard
{
    /// <summary>
    /// Atomically claims <paramref name="messageKey"/>. Returns <c>true</c> when the key was already
    /// claimed (the effect already happened → the caller acks without acting), <c>false</c> when this
    /// call won the claim (the caller proceeds with its terminal effect).
    /// </summary>
    Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default);
}
