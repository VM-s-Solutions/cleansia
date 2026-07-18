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

    public Task<RefreshToken?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters(): see GetByTokenHashAsync — chain rows are null-stamped.
        return context.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
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

    public async Task<int> BulkRevokeIgnoringConcurrencyAsync(RefreshTokenRevocationScope scope, string reason, CancellationToken cancellationToken)
    {
        if (scope.TokenHash is null && scope.UserId is null)
        {
            throw new ArgumentException("Revocation scope must set TokenHash or UserId", nameof(scope));
        }

        // Revoke-then-VERIFY loop. A single set-based UPDATE is not enough: under READ COMMITTED a
        // rotation whose flush already passed its parent-xmin check (row locked, uncommitted) when our
        // statement takes its snapshot escapes BOTH classic race sides — the UPDATE blocks on the
        // parent, EPQ skips it post-commit as already-rotated, and the freshly-committed child was
        // invisible to our snapshot. Each loop pass is a NEW statement (fresh snapshot), so a child
        // that slipped pass N is visible to pass N+1; once a verification read on its own fresh
        // snapshot finds ZERO live rows in scope, any rotation still in flight read its parent before
        // our committed revoke bumped that parent's xmin, so its own flush fails and rolls its child
        // back — the kill switch has provably terminated the scope. Escaping the cap needs an
        // attacker to statement-overlap every pass back-to-back (a full rotation round trip inside
        // each single-UPDATE window, MaxPasses times) — and rather than silently reporting success
        // with a live token, hitting the cap throws (500, sibling changes roll back, everything
        // revoked so far stays revoked: the residual failure mode is availability, never a silent
        // live session).
        //
        // Widening note: unlike GetActiveByUserIdAsync-based tracked paths, the predicate has no
        // ExpiresAt filter, so expired-but-unrevoked rows get stamped too. Security-neutral (they are
        // already dead to every reader) and it cannot perturb the ADR-0027 reset instant (MAX(RevokedAt)
        // moves together); accepted so the scope predicate stays exactly restatable in SQL.
        const int maxPasses = 8;
        var total = 0;
        for (var pass = 1; pass <= maxPasses; pass++)
        {
            var now = DateTimeOffset.UtcNow;
            total += await BuildScopeQuery(scope).ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevokedReason, reason),
                cancellationToken);

            if (!await BuildScopeQuery(scope).AnyAsync(cancellationToken))
            {
                return total;
            }
        }

        throw new InvalidOperationException(
            $"Refresh-token kill switch could not verify termination after {maxPasses} bulk passes — " +
            "live tokens keep appearing in scope faster than they can be revoked. Failing the command " +
            "rather than reporting a revocation that provably did not complete.");
    }

    private IQueryable<RefreshToken> BuildScopeQuery(RefreshTokenRevocationScope scope)
    {
        // IgnoreQueryFilters(): same null-TenantId rationale as GetByTokenHashAsync — the kill switch
        // must reach the caller's own null-stamped rows on a tenant-claimed request. Scope stays bound
        // to the caller's own hash/user id, never widening. The RevokedAt == null guard makes the
        // update idempotent and preserves committed forensic marks (rotated/ReplacedByTokenId chains).
        var query = context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.RevokedAt == null);

        if (scope.TokenHash is not null)
        {
            query = query.Where(t => t.TokenHash == scope.TokenHash);
        }
        if (scope.UserId is not null)
        {
            query = query.Where(t => t.UserId == scope.UserId);
        }
        if (scope.DeviceId is not null)
        {
            // A non-null constant on the right means a NULL DeviceId row can never match — the same
            // load-bearing null-guard as RevokeByDeviceAsync's tracked path.
            query = query.Where(t => t.DeviceId == scope.DeviceId);
        }
        if (scope.SparedTokenHash is not null)
        {
            query = query.Where(t => t.TokenHash != scope.SparedTokenHash);
        }

        return query;
    }

    public void DetachModifiedTracked()
    {
        // Only Modified entries (the stale staged revoke marks) — an Added entry is a freshly issued
        // token that must still insert with the command's later commit.
        foreach (var entry in context.ChangeTracker.Entries<RefreshToken>()
                     .Where(e => e.State == EntityState.Modified)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
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
