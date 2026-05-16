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
}
