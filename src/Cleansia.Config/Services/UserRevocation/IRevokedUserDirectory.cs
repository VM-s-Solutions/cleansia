namespace Cleansia.Config.Services.UserRevocation;

/// <summary>
/// The in-memory revoked-user snapshot (ADR-0027 D1). The request path asks <see cref="IsRevoked"/>
/// — a pure O(1) dictionary lookup, zero I/O — and the background refresher feeds it via
/// <see cref="Replace"/>. A structural sibling of <c>IRevokedDeviceDirectory</c>, one key narrower
/// (<c>userId</c>, not the device composite): a password RESET is keep-none, so there is no session to
/// spare and userId alone is the right key. The seam is deliberately swappable: a literal-zero
/// escalation swaps in a read-through implementation behind this interface without touching hosts or
/// claims.
/// </summary>
public interface IRevokedUserDirectory
{
    /// <summary>
    /// True iff a reset entry exists for <paramref name="userId"/> and the session was established at
    /// or before it — i.e. <paramref name="tokenIssuedAt"/> is null (an unprovable-age token, A2) or
    /// strictly precedes the recorded reset. A token minted after the reset (the post-reset re-login)
    /// passes even while the entry is present — reset is a session kill, not a user ban.
    /// </summary>
    bool IsRevoked(string userId, DateTimeOffset? tokenIssuedAt);

    /// <summary>Atomically replaces the served snapshot (called only by the refresher on a good poll).</summary>
    void Replace(IReadOnlyCollection<RevokedUserEntry> entries, DateTimeOffset polledAt);
}

/// <summary>One revoked-user row as the directory keys it: the user plus their most-recent reset instant.</summary>
public sealed record RevokedUserEntry(string UserId, DateTimeOffset ResetAt);
