using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class RefreshTokenRepository(CleansiaDbContext context)
    : BaseRepository<RefreshToken>(context), IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        // Intentionally ignores IsActive and RevokedAt — callers need to see revoked
        // tokens to detect rotation reuse.
        return context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> RevokeChainAsync(string rootTokenId, string reason, CancellationToken cancellationToken)
    {
        // Walk backwards and forwards from the root — tokens that point TO this id
        // (rootTokenId in ReplacedByTokenId of older tokens) plus descendants.
        // Simpler: find the UserId, then revoke every non-revoked token for that user.
        // Any theft signal is serious enough to warrant force-logout-everywhere anyway.
        var root = await context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Id == rootTokenId, cancellationToken);
        if (root is null) return 0;

        var now = DateTimeOffset.UtcNow;
        var tokensToRevoke = await context.RefreshTokens
            .Where(t => t.UserId == root.UserId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokensToRevoke)
        {
            token.Revoke(reason, now);
        }
        return tokensToRevoke.Count;
    }

    public Task<int> DeleteStaleAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
    {
        // Revoked tokens: delete when their revocation is older than the cutoff.
        // Never-revoked-but-expired tokens: delete when their expiry is older than the cutoff.
        // We keep recently-revoked tokens to preserve rotation-theft detection history.
        // Bypasses the tenant filter — system job, no JWT context.
        return context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t =>
                (t.RevokedAt != null && t.RevokedAt <= olderThan) ||
                (t.RevokedAt == null && t.ExpiresAt <= olderThan))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
