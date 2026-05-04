using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface ITokenService
{
    /// <summary>
    /// Issues a short-lived access token (JWT, 15 min) plus a long-lived refresh token
    /// (30d if rememberMe=true, 1d otherwise). Both are returned in <see cref="JwtTokenResponse"/>;
    /// the raw refresh token string is returned to the caller exactly once and then
    /// irretrievable — server stores only a SHA-256 hash.
    ///
    /// Device label + IP for the refresh-token audit trail are pulled from the
    /// current request via <c>IRequestMetadataProvider</c>.
    ///
    /// Returns an unconfirmed response (empty Token, null RefreshToken) when the user
    /// has not yet confirmed their email.
    /// </summary>
    JwtTokenResponse GenerateToken(User user, bool rememberMe);
}