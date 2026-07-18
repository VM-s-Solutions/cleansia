using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IRefreshTokenRepository : IRepository<RefreshToken, string>
{
    /// <summary>
    /// Looks up a refresh token by its SHA-256 hash. Includes revoked and expired
    /// tokens — callers must check <see cref="RefreshToken.IsAlive"/> or inspect
    /// <c>RevokedAt</c>/<c>ExpiresAt</c> themselves. Returning even revoked tokens
    /// is intentional: rotation-theft detection needs to recognise a previously
    /// valid but now-revoked token.
    /// </summary>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Point lookup by primary key that bypasses the tenant filter — the successor-chain walk
    /// follows <c>ReplacedByTokenId</c> links across rows that are null-TenantId-stamped (same
    /// rationale as <see cref="GetByTokenHashAsync"/>: the ambient filter would hide the caller's
    /// own rows on a tenant-claimed request).
    /// </summary>
    Task<RefreshToken?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken);

    /// <summary>All non-revoked, non-expired tokens for a user. Used by "log out everywhere".</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// When rotation reuse is detected, walk the <c>ReplacedByTokenId</c> chain and
    /// revoke every token belonging to the same refresh chain. Returns the number
    /// of tokens revoked (for logging / alerting).
    /// </summary>
    Task<int> RevokeChainAsync(string rootTokenId, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Hard-deletes refresh tokens that are both (a) revoked OR expired, and
    /// (b) at least as old as <paramref name="olderThan"/>. Returns rows deleted.
    /// Called nightly by the cleanup function.
    /// </summary>
    Task<int> DeleteStaleAsync(DateTimeOffset olderThan, CancellationToken cancellationToken);

    /// <summary>
    /// The fail-closed last resort behind the revoke retry loops (T-0421a): a SET-BASED revoke that
    /// re-expresses the caller's target predicate in SQL and deliberately bypasses the xmin optimistic-
    /// concurrency check (a bulk UPDATE never reads a stale row version, so no rotation can outrace it
    /// into a 500). Because one statement's snapshot can still miss the child of a rotation whose
    /// flush OVERLAPS the statement (parent locked, child uncommitted-invisible), the implementation
    /// loops revoke-then-verify on fresh snapshots until a verification read proves ZERO live rows in
    /// scope — at that point any still-in-flight rotation read its parent before the committed revoke
    /// bumped its xmin, so its own flush fails and rolls its child back. Throws (rather than reporting
    /// a revocation that provably did not complete) if scope termination cannot be verified within the
    /// pass cap. Only rows with <c>RevokedAt IS NULL</c> are touched (idempotent; committed
    /// revocations, including forensic "rotated" marks with their ReplacedByTokenId chain pointers,
    /// are never overwritten). Skips the audit-stamp interceptor (UpdatedOn/UpdatedBy stay stale) —
    /// the operative columns every reader checks are RevokedAt/RevokedReason. Returns rows revoked.
    /// </summary>
    Task<int> BulkRevokeIgnoringConcurrencyAsync(RefreshTokenRevocationScope scope, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Drops every tracked-but-unflushed RefreshToken MODIFICATION (staged revoke marks whose xmin went
    /// stale after repeated collisions) so a later commit of the command's sibling staged changes can no
    /// longer collide on this entity type. Added (freshly issued) token entities are deliberately
    /// preserved. Fail-closed support for <see cref="BulkRevokeIgnoringConcurrencyAsync"/> (T-0421a),
    /// which supersedes the dropped marks with the set-based revoke.
    /// </summary>
    void DetachModifiedTracked();

    /// <summary>
    /// The RevokedUserDirectory poll source (ADR-0027): for every user with at least one
    /// <c>password_reset</c> revocation at or after <paramref name="cutoff"/>, the instant of their
    /// MOST-RECENT such revocation, projected to <see cref="UserPasswordReset"/>. The predicate is
    /// <c>RevokedReason == "password_reset"</c> ALONE — never <c>password_changed</c> (ADR-0027 D3:
    /// change is authenticated hygiene, not takeover recovery, and feeding it would self-inflict a 401
    /// on the change caller's own spared session). <c>MAX(RevokedAt)</c> per user is the reset instant,
    /// robust to a second reset inside the horizon (latest-wins). No new schema — the reset already
    /// persisted this timestamped signal (T-0407).
    /// </summary>
    Task<IReadOnlyList<UserPasswordReset>> GetPasswordResetsSinceAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}

/// <summary>Projection for the user-revocation poll (ADR-0027): the user and their most-recent password-reset instant.</summary>
public sealed record UserPasswordReset(string UserId, DateTimeOffset ResetAt);

/// <summary>
/// The target predicate of a fail-closed bulk revoke (T-0421a), mirroring the four tracked revoke
/// paths. At least one of <see cref="TokenHash"/> / <see cref="UserId"/> must be set; every non-null
/// member narrows the match (AND semantics).
/// </summary>
public sealed record RefreshTokenRevocationScope
{
    /// <summary>Single-token scope (logout): the SHA-256 hash of the one token to end.</summary>
    public string? TokenHash { get; init; }

    /// <summary>User-wide scope (password change/reset, theft-chain): every live token of this user.</summary>
    public string? UserId { get; init; }

    /// <summary>With <see cref="UserId"/>: narrow to one device's sessions. A row with a NULL DeviceId never matches (same null-guard as the tracked path).</summary>
    public string? DeviceId { get; init; }

    /// <summary>With <see cref="UserId"/>: spare the caller's own session (ADR-0024 D4.6 revoke-all-except).</summary>
    public string? SparedTokenHash { get; init; }
}
