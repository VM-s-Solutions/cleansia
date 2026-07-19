using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Users;

/// <summary>
/// A server-side refresh token belonging to a user. Stored as a SHA-256 hash —
/// the raw token is returned to the client exactly once at issue time and never
/// retrievable again. Rotation creates a new row and marks the previous one
/// revoked with <see cref="RevokedReason"/> = "rotated" and
/// <see cref="ReplacedByTokenId"/> set. Reuse of a rotated token is treated as
/// a theft signal and revokes the entire chain.
/// </summary>
public class RefreshToken : Auditable, ITenantEntity
{
    public string UserId { get; private set; } = default!;
    public User? User { get; private set; }

    /// <summary>SHA-256 hex digest of the raw refresh token. Indexed unique.</summary>
    [Required]
    [MaxLength(64)]
    public string TokenHash { get; private set; } = default!;

    /// <summary>Sliding-window expiry. Extended on every rotation.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>One of: "rotated", "logout", "logout_chain" (a rotated token's successors
    /// revoked because their owner logged out with the stale parent — deliberately distinct from
    /// "rotated", which drives reuse-theft detection, and from "password_reset", the ADR-0027 poll
    /// predicate), "admin", "security", "device_revoked", "password_changed", "password_reset".</summary>
    [MaxLength(20)]
    public string? RevokedReason { get; private set; }

    /// <summary>When this token is rotated, points to the new token's Id — for forensic chains.</summary>
    [MaxLength(26)]
    public string? ReplacedByTokenId { get; private set; }

    /// <summary>Best-effort human-readable label: "Pixel 9 Pro · Android 15", "Chrome 120 · macOS".</summary>
    [MaxLength(120)]
    public string? DeviceLabel { get; private set; }

    /// <summary>
    /// Stable per-install device id (the same value the app registers as
    /// <see cref="Cleansia.Core.Domain.Devices.Device.DeviceId"/>), captured at
    /// issue/rotation time from the <c>X-Device-Id</c> header. This — not the
    /// human-readable <see cref="DeviceLabel"/> — is the key a per-device revoke
    /// matches on. Null for clients that don't send the header (e.g. web), which
    /// makes those tokens non-matchable by device revoke (they age out at expiry).
    /// </summary>
    [MaxLength(64)]
    public string? DeviceId { get; private set; }

    [MaxLength(45)] // IPv6 max length
    public string? IpAddress { get; private set; }

    /// <summary>JWT audience the refresh token is bound to. On rotation, the new
    /// access token is issued with the same audience so a token can't be transplanted
    /// to a different host.</summary>
    [MaxLength(40)]
    public string? Audience { get; private set; }

    public bool IsAlive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public static RefreshToken Create(
        string userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string audience,
        string? deviceLabel,
        string? ipAddress,
        string? deviceId = null)
        => new()
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            Audience = audience,
            DeviceLabel = deviceLabel,
            IpAddress = ipAddress,
            DeviceId = deviceId,
        };

    public RefreshToken MarkUsed(DateTimeOffset at)
    {
        LastUsedAt = at;
        return this;
    }

    public RefreshToken Revoke(string reason, DateTimeOffset at, string? replacedByTokenId = null)
    {
        RevokedAt = at;
        RevokedReason = reason;
        ReplacedByTokenId = replacedByTokenId;
        return this;
    }
}
