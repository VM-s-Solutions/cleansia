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
