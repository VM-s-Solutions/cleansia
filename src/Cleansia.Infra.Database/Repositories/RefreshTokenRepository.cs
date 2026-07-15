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
        //
        // IgnoreQueryFilters(): tokens are issued/rotated on the ANONYMOUS login/refresh path with no
        // tenant claim, so their rows are stamped TenantId == null; the revoke/rotate read runs on an
        // AUTHENTICATED request whose JWT may carry a tenant_id. The tenant filter would then hide the
        // user's own null-stamped row, so logout/rotation-reuse detection would silently match nothing.
        // The unguessable SHA-256 hash is the scope — it can never match another tenant's token.
        return context.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        // IgnoreQueryFilters() + the explicit UserId predicate: see GetByTokenHashAsync. The UserId
        // (taken from the caller's own JWT) keeps the read scoped to the requesting user's own rows,
        // never widening across tenants — it just stops the filter hiding their null-stamped tokens.
        return await context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> RevokeChainAsync(string rootTokenId, string reason, CancellationToken cancellationToken)
    {
        // Walk backwards and forwards from the root — tokens that point TO this id
        // (rootTokenId in ReplacedByTokenId of older tokens) plus descendants.
        // Simpler: find the UserId, then revoke every non-revoked token for that user.
        // Any theft signal is serious enough to warrant force-logout-everywhere anyway.
        // IgnoreQueryFilters() + UserId scope: see GetByTokenHashAsync — the theft-signal chain revoke
        // must reach the user's null-stamped tokens even on a tenant-claimed request, scoped to that user.
        var root = await context.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == rootTokenId, cancellationToken);
        if (root is null) return 0;

        var now = DateTimeOffset.UtcNow;
        var tokensToRevoke = await context.RefreshTokens
            .IgnoreQueryFilters()
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

    public async Task<IReadOnlyList<UserPasswordReset>> GetPasswordResetsSinceAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        // Background, tenant-less, cross-tenant-by-design read (the sanctioned T-0245 pattern): the
        // directory refresher has no JWT context and user ids are globally unique, so IgnoreQueryFilters
        // is required and safe (see GetByTokenHashAsync). Reason is "password_reset" ALONE (never
        // "password_changed" — ADR-0027 D3). The RevokedAt != null guard makes the .Value inside Max
        // non-null before the group-by aggregate translates server-side.
        return await context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.RevokedReason == "password_reset" && t.RevokedAt != null && t.RevokedAt >= cutoff)
            .GroupBy(t => t.UserId)
            .Select(g => new UserPasswordReset(g.Key, g.Max(t => t.RevokedAt!.Value)))
            .ToListAsync(cancellationToken);
    }
}
