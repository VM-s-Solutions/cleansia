namespace Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;

// `CsrfToken` is populated by the web auth controllers (Customer / Partner /
// Admin) when CSRF protection is enabled. Web clients keep it in memory and
// echo it back as `X-CSRF-Token` on state-changing requests. Mobile leaves
// it null — mobile authenticates via `Authorization: Bearer` which is
// unforgeable by CSRF and doesn't need the second factor.
//
// `Role` is the user's profile (Customer / Employee / Administrator) emitted
// explicitly so web clients can drive permission gating without decoding
// the JWT — which becomes impossible once the access token is HttpOnly.
// Source-of-truth is still server-side; this is a UI hint only.
public record JwtTokenResponse(
    string Token,
    bool IsEmailConfirmed,
    bool HasAdminAccess = true,
    string? UserId = null,
    string? Email = null,
    string? RefreshToken = null,
    DateTimeOffset? RefreshTokenExpiresAt = null,
    string? CsrfToken = null,
    string? Role = null);
