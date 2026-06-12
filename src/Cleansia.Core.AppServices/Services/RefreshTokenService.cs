using System.Security.Cryptography;
using System.Text;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
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
            // would be silently rolled back and every stolen-chain token would stay valid.
            await unitOfWork.CommitAsync(cancellationToken);
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

    public async Task RevokeAsync(string rawToken, string reason, CancellationToken cancellationToken)
    {
        var hash = HashToken(rawToken);
        var existing = await repository.GetByTokenHashAsync(hash, cancellationToken);
        if (existing is null || existing.RevokedAt is not null)
        {
            // Silently succeed on unknown/already-revoked — prevents token-probing attacks
            // and logout is idempotent.
            return;
        }
        existing.Revoke(reason, DateTimeOffset.UtcNow);
    }

    public async Task RevokeByDeviceAsync(string userId, string deviceId, string reason, CancellationToken cancellationToken)
    {
        var active = await repository.GetActiveByUserIdAsync(userId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        // Match on the stable device id captured at issue/rotation time. The null-guard is
        // load-bearing: a token with no DeviceId (pre-existing, or a client that never sent
        // X-Device-Id) must never match, so it survives until natural expiry rather than
        // being revoked by an unrelated device. Reuse the same Revoke mutation logout uses;
        // the UnitOfWork pipeline commits.
        foreach (var token in active.Where(t => t.DeviceId != null && t.DeviceId == deviceId))
        {
            token.Revoke(reason, now);
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
