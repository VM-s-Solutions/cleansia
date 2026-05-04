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
    IssuedRefreshToken Issue(string userId, bool rememberMe, string? deviceLabel = null, string? ipAddress = null);

    /// <summary>
    /// Validates a raw refresh token, rotates it, and returns the new token. Throws
    /// <see cref="RefreshTokenValidationException"/> on invalid / expired / revoked input.
    /// If rotation-reuse is detected (an already-rotated token presented again), revokes
    /// the entire chain and throws with <see cref="RefreshTokenValidationException.IsTheftSignal"/> = true.
    /// </summary>
    Task<IssuedRefreshToken> RotateAsync(string rawToken, string? deviceLabel, string? ipAddress, CancellationToken cancellationToken);

    /// <summary>Revokes a single refresh token. Used by logout. Silently no-ops on unknown/revoked tokens.</summary>
    Task RevokeAsync(string rawToken, string reason, CancellationToken cancellationToken);

    /// <summary>Hashes a raw token using SHA-256 hex — exposed for tests and for the validator that checks existence.</summary>
    string HashToken(string rawToken);
}

public class RefreshTokenValidationException(string message, bool isTheftSignal = false)
    : Exception(message)
{
    public bool IsTheftSignal { get; } = isTheftSignal;
}
