using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Raw refresh token value + its server-side record, returned at issue time.
/// The raw value is returned to the client exactly once and never retrievable again.
/// </summary>
public record IssuedRefreshToken(string RawToken, RefreshToken Record);

public interface IRefreshTokenService
{
    /// <summary>Creates a new refresh token for a user and returns both the raw value and the entity.</summary>
    IssuedRefreshToken Issue(string userId, bool rememberMe, string audience, string? deviceLabel = null, string? ipAddress = null, string? deviceId = null);

    /// <summary>
    /// Validates a raw refresh token, rotates it, and returns the new token. Throws
    /// <see cref="RefreshTokenValidationException"/> on invalid / expired / revoked input.
    /// If rotation-reuse is detected (an already-rotated token presented again), revokes
    /// the entire chain and throws with <see cref="RefreshTokenValidationException.IsTheftSignal"/> = true.
    /// The rotated token inherits the existing row's device id (header is a fallback only for
    /// pre-existing rows rotated for the first time) so a rotated session stays revocable.
    /// </summary>
    Task<IssuedRefreshToken> RotateAsync(string rawToken, string? deviceLabel, string? ipAddress, CancellationToken cancellationToken, string? deviceId = null);

    /// <summary>Revokes a single refresh token. Used by logout. Silently no-ops on unknown/revoked tokens.</summary>
    Task RevokeAsync(string rawToken, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes every active refresh token a user holds for a given device, via the same
    /// <see cref="RefreshToken.Revoke"/> mutation logout uses, so a revoked handset can no
    /// longer mint access tokens on its next refresh. No-ops when nothing matches.
    /// </summary>
    Task RevokeByDeviceAsync(string userId, string deviceId, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes every active refresh token a user holds — the credential-rotation kill switch
    /// (ADR-0024 D4.6: without this, a hijacker's refresh chain outlives a password change).
    /// <paramref name="exceptRawToken"/>, when supplied, spares exactly the session performing
    /// the change: a password CHANGE passes the caller's own refresh token so they stay signed
    /// in; a password RESET passes null (the caller proves control via the emailed code, not a
    /// live session — after a takeover the attacker's sessions are the very thing being killed).
    /// Persistence rides the caller's unit of work. No-ops when nothing matches.
    /// </summary>
    Task RevokeAllForUserAsync(string userId, string reason, string? exceptRawToken, CancellationToken cancellationToken);

    /// <summary>Hashes a raw token using SHA-256 hex — exposed for tests and for the validator that checks existence.</summary>
    string HashToken(string rawToken);
}

public class RefreshTokenValidationException(string message, bool isTheftSignal = false)
    : Exception(message)
{
    public bool IsTheftSignal { get; } = isTheftSignal;
}
