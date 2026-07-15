using System.Security.Cryptography;
using System.Text;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

public class RefreshTokenService(
    IRefreshTokenRepository repository,
    IUnitOfWork unitOfWork,
    IJwtSettings jwtSettings,
    ILogger<RefreshTokenService> logger)
    : IRefreshTokenService
{
    // 48 bytes = 384 bits of entropy → 64 base64url chars. Overkill; that's the point.
    private const int TokenByteLength = 48;

    public IssuedRefreshToken Issue(string userId, bool rememberMe, string audience, string? deviceLabel = null, string? ipAddress = null, string? deviceId = null)
    {
        var raw = GenerateRawToken();
        var hash = HashToken(raw);
        var lifetime = rememberMe
            ? TimeSpan.FromDays(jwtSettings.RefreshTokenExpDays)
            : TimeSpan.FromDays(jwtSettings.RefreshTokenShortExpDays);

        var record = RefreshToken.Create(
            userId: userId,
            tokenHash: hash,
            expiresAt: DateTimeOffset.UtcNow.Add(lifetime),
            audience: audience,
            deviceLabel: deviceLabel,
            ipAddress: ipAddress,
            deviceId: deviceId);

        repository.Add(record);
        return new IssuedRefreshToken(raw, record);
    }

    public async Task<IssuedRefreshToken> RotateAsync(string rawToken, string? deviceLabel, string? ipAddress, CancellationToken cancellationToken, string? deviceId = null)
    {
        var hash = HashToken(rawToken);
        var existing = await repository.GetByTokenHashAsync(hash, cancellationToken);

        if (existing is null)
        {
            throw new RefreshTokenValidationException("Refresh token not found");
        }

        // Rotation-reuse detection: a token that was already rotated is being presented again.
        // This means either (a) the legitimate client retried because they didn't get the response
        // (unlikely with proper client code), or (b) the token was stolen. Either way, the safe
        // action is to revoke the entire chain for this user.
        if (existing.RevokedAt is not null && existing.RevokedReason == "rotated")
        {
            var count = await repository.RevokeChainAsync(existing.Id, "security", cancellationToken);
            // Persist the chain revocation NOW — independently of the caller. The handler turns this
            // theft signal into a BusinessResult.Failure (401), and UnitOfWorkPipelineBehavior commits
            // ONLY on success (ADR-0002 D4). Without this explicit commit the security revocation
            // would be silently rolled back and every stolen-chain token would stay valid. Retry on an
            // xmin collision (a rotation racing this chain revoke) so it can't be outraced into a 500.
            await CommitStagedChainRevokeWithRetryAsync("security", cancellationToken);
            logger.LogWarning(
                "Refresh token rotation-reuse detected for user {UserId}. Revoked {Count} tokens in the chain.",
                existing.UserId, count);
            throw new RefreshTokenValidationException(
                "Refresh token reuse detected — all sessions revoked for security.",
                isTheftSignal: true);
        }

        if (existing.RevokedAt is not null)
        {
            throw new RefreshTokenValidationException($"Refresh token revoked ({existing.RevokedReason})");
        }

        if (existing.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new RefreshTokenValidationException("Refresh token expired");
        }

        // Issue a new token with a fresh sliding-window expiry. Use the same rememberMe
        // semantics: if the original token had the long (30d) lifetime, keep it long;
        // if it was the short (1d) kind, keep it short. We infer from the original's
        // lifetime at creation time — a token within RefreshTokenShortExpDays of now
        // was issued as "short", otherwise "long".
        var originalLifetime = existing.ExpiresAt - existing.CreatedOn;
        var rememberMe = originalLifetime.TotalDays > jwtSettings.RefreshTokenShortExpDays + 0.5;

        // Note: Issue() calls repository.Add for the NEW token. We need to get its Id
        // before revoking the old one so we can set ReplacedByTokenId for the forensic chain.
        // Audience is preserved across rotation so the new token stays bound to the same host.
        var audience = existing.Audience ?? string.Empty;
        // Inherit the device id so a rotated session stays revocable. The DB value wins;
        // the header is only a fallback for a pre-existing row being rotated for the first
        // time after this ships (its stored DeviceId is still null).
        var carriedDeviceId = existing.DeviceId ?? deviceId;
        var issued = Issue(existing.UserId, rememberMe, audience, deviceLabel, ipAddress, carriedDeviceId);

        existing.MarkUsed(DateTimeOffset.UtcNow);
        existing.Revoke("rotated", DateTimeOffset.UtcNow, replacedByTokenId: issued.Record.Id);

        return issued;
    }

    public async Task CommitRotationAsync(CancellationToken cancellationToken)
    {
        // Fail closed against a revoke that raced this rotation. The parent row carries an xmin
        // concurrency token; a concurrent RevokeByDeviceAsync/RevokeAllForUserAsync that committed
        // between the rotation's read and this flush changed its xmin, so the commit throws and rolls
        // back BOTH the parent's "rotated" mark AND the freshly-issued child insert — the revoke wins
        // and no escaped token survives. The caller flushes here (after its own accept/reject gates,
        // so a rejected refresh never persists a rotation) rather than deferring to the UnitOfWork
        // pipeline, so the collision is caught where it becomes an auth failure instead of a 500
        // surfacing from the pipeline commit (S7b). The pipeline's later commit is then a safe no-op.
        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new RefreshTokenValidationException("Refresh token rotation lost a race with a concurrent revoke");
        }
    }

    public async Task RevokeAsync(string rawToken, string reason, CancellationToken cancellationToken)
    {
        var hash = HashToken(rawToken);
        // Idempotent, retry-on-conflict revoke: a token carries an xmin concurrency token, so a
        // concurrent rotation of the same row would make the commit throw. A revoke ending the token
        // dead is the whole goal, so we retry to success rather than surfacing a 500 (S7a idempotency).
        await CommitRevokeWithRetryAsync(reason, async (revoke, ct) =>
        {
            var existing = await repository.GetByTokenHashAsync(hash, ct);
            if (existing is null || existing.RevokedAt is not null)
            {
                // Silently succeed on unknown/already-revoked — prevents token-probing attacks
                // and logout is idempotent.
                return;
            }
            revoke(existing);
        }, cancellationToken);
    }

    public async Task RevokeByDeviceAsync(string userId, string deviceId, string reason, CancellationToken cancellationToken)
    {
        // Match on the stable device id captured at issue/rotation time. The null-guard is
        // load-bearing: a token with no DeviceId (pre-existing, or a client that never sent
        // X-Device-Id) must never match, so it survives until natural expiry rather than
        // being revoked by an unrelated device. Retry-on-conflict (see RevokeAsync).
        await CommitRevokeWithRetryAsync(reason, async (revoke, ct) =>
        {
            var active = await repository.GetActiveByUserIdAsync(userId, ct);
            foreach (var token in active.Where(t => t.DeviceId != null && t.DeviceId == deviceId))
            {
                revoke(token);
            }
        }, cancellationToken);
    }

    public async Task RevokeAllForUserAsync(string userId, string reason, string? exceptRawToken, CancellationToken cancellationToken)
    {
        var sparedHash = string.IsNullOrWhiteSpace(exceptRawToken) ? null : HashToken(exceptRawToken);
        // The account-takeover recovery kill switch (ADR-0024 D4.6). Retry-on-conflict is essential
        // here: an attacker keeping a stolen token refreshing must NOT be able to force a collision that
        // 500s the password reset and rolls the revoke-all back. A revoke is idempotent, so we retry
        // until every targeted token is dead (see RevokeAsync).
        await CommitRevokeWithRetryAsync(reason, async (revoke, ct) =>
        {
            var active = await repository.GetActiveByUserIdAsync(userId, ct);
            foreach (var token in active.Where(t => sparedHash is null || t.TokenHash != sparedHash))
            {
                revoke(token);
            }
        }, cancellationToken);
    }

    // Stages a revoke (via the caller's predicate) then commits, riding the caller's own unit of work
    // so a revoke stays atomic with any sibling change the command already staged (e.g. the new password
    // hash in ChangePassword — ADR-0024 D4.6). A target RefreshToken carries an xmin optimistic-
    // concurrency token, so a rotation that concurrently touched one of the target rows makes the commit
    // throw DbUpdateConcurrencyException. Revocation is idempotent — the token ends dead either way — so
    // instead of surfacing a 500 (and rolling the revoke, and any sibling change, back) we recover: reload
    // the conflicted rows to clear the stale xmin, then RE-RUN the stage predicate. Re-running (not just
    // re-applying to the conflicted entries) is what makes the kill switch airtight against the very race
    // it guards — a rotation that raced RevokeAllForUser inserts a NEW child token the first read never
    // saw; the re-read picks it up and revokes it too, so no rotation child escapes the revoke-all.
    // Reloading only the conflicted RefreshToken entries (never clearing the tracker) keeps the command's
    // other staged changes intact, so atomicity holds. Self-committing here rather than deferring to the
    // pipeline is what lets the conflict be caught and retried; the pipeline's later commit is a no-op.
    private async Task CommitRevokeWithRetryAsync(
        string reason,
        Func<Action<RefreshToken>, CancellationToken, Task> stage,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            var now = DateTimeOffset.UtcNow;
            await stage(token => token.Revoke(reason, now), cancellationToken);
            try
            {
                await unitOfWork.CommitAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxAttempts)
            {
                // Reload the conflicted rows to their committed state (fresh xmin) so the next attempt's
                // re-read and re-stage build on current data instead of the stale tracked instances.
                foreach (var entry in ex.Entries)
                {
                    await entry.ReloadAsync(cancellationToken);
                }
            }
        }
    }

    // Commit-only retry for a revoke that was already staged by the repository (the rotation-reuse chain
    // revoke). Same idempotent fail-to-success contract; the conflicted rows are reloaded and re-revoked.
    private async Task CommitStagedChainRevokeWithRetryAsync(string reason, CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;
        var now = DateTimeOffset.UtcNow;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await unitOfWork.CommitAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxAttempts)
            {
                foreach (var entry in ex.Entries)
                {
                    await entry.ReloadAsync(cancellationToken);
                    if (entry.Entity is RefreshToken { RevokedAt: null } token)
                    {
                        token.Revoke(reason, now);
                    }
                }
            }
        }
    }

    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
