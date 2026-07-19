namespace Cleansia.Infra.Clients.Apns;

/// <summary>
/// Mints and caches the APNs provider authentication token — an ES256 JWT signed with the team's
/// <c>.p8</c> key (ADR-0029 D1). Apple accepts a provider token for 20–60 min and RATE-LIMITS
/// re-minting, so the token is cached and re-minted only when it ages past the safe window (~50 min)
/// or after <see cref="Invalidate"/> (a <c>403 ExpiredProviderToken</c>).
/// </summary>
public interface IApnsJwtProvider
{
    /// <summary>The current provider JWT — minted on first use, then cached until it ages out.</summary>
    string GetToken();

    /// <summary>Drops the cached token so the next <see cref="GetToken"/> re-mints (after a 403).</summary>
    void Invalidate();

    /// <summary>
    /// <c>false</c> when the configured key material cannot be parsed into a signing key — a truncated /
    /// corrupt <c>.p8</c>, or an unresolved <c>@Microsoft.KeyVault(SecretUri=…)</c> reference App Service
    /// hands through verbatim when the secret is not yet seeded. Lets the client degrade to the same
    /// Skipped path as keyless (ADR-0029 D1) instead of a <see cref="GetToken"/> throw poison-storming the
    /// dispatch queue. Never throws; never surfaces the key material (S6).
    /// </summary>
    bool HasUsableKey();
}
