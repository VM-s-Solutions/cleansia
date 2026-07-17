using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cleansia.Config.Authentication;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Users;
using Microsoft.IdentityModel.Tokens;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// The CSRF token is derived from a session key that must ROTATE per token issuance, not be pinned to
/// the stable user id (T-0356). SetClaims now emits a unique <c>jti</c>; the issuance side
/// (AuthCookieConfig.SessionKeyForCsrf) reads it from the token and the validation side
/// (CsrfTokenService.GetSessionKey) reads it from the validated principal. These pin that both sides
/// resolve the SAME jti — so a CSRF token derived at issuance still validates — and that it rotates.
/// </summary>
public class CsrfSessionKeyRotationTests
{
    private static User NewUser()
    {
        var user = User.CreateWithPassword("csrf.user@cleansia.test", "Passw0rd!", "Csrf", "User");
        user.Id = "user-csrf-1";
        return user;
    }

    [Fact]
    public void SetClaims_EmitsAUniqueJti_ThatRotatesPerIssuance()
    {
        var user = NewUser();

        var jti1 = user.SetClaims().First(c => c.Type == "jti").Value;
        var jti2 = user.SetClaims().First(c => c.Type == "jti").Value;

        Assert.False(string.IsNullOrEmpty(jti1));
        Assert.NotEqual(jti1, jti2); // rotates — not pinned to the stable user id
    }

    [Fact]
    public void CsrfSessionKey_IssuanceAndValidation_ResolveTheSameJti_FromTheToken()
    {
        var user = NewUser();
        var claims = user.SetClaims().ToList();
        var jti = claims.First(c => c.Type == "jti").Value;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 64)));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "cleansia.tests",
            audience: "customer",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));

        // Issuance side (AuthCookieWriter): SessionKeyForCsrf reads the jti out of the issued token.
        var config = new AuthCookieConfig { AccessCookieName = "customer_token", RefreshCookieName = "customer_refresh" };
        var response = new JwtTokenResponse(Token: token, IsEmailConfirmed: true, UserId: user.Id);
        var issuanceKey = config.SessionKeyForCsrf(response);
        Assert.Equal(jti, issuanceKey);
        Assert.NotEqual(user.Id, issuanceKey); // NOT the pinned user id anymore

        // Validation side (CsrfValidationMiddleware): validate the token, GetSessionKey reads its jti.
        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                IssuerSigningKey = key,
            },
            out _);
        var validationKey = CsrfTokenService.GetSessionKey(principal);

        // Both sides resolve the same jti -> the CSRF token derived at issuance round-trips.
        Assert.Equal(jti, validationKey);
        Assert.Equal(issuanceKey, validationKey);
    }
}
