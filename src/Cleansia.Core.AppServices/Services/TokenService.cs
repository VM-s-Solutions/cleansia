using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using System.IdentityModel.Tokens.Jwt;
using Cleansia.Infra.Common.Configuration.Interfaces;

namespace Cleansia.Core.AppServices.Services;

public class TokenService(
    IJwtSettings jwtSettings,
    IRefreshTokenService refreshTokenService,
    IEmployeeRepository employeeRepository,
    IRequestMetadataProvider requestMetadata,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider)
    : ITokenService
{
    // How far to backdate a freshly-minted access token's NotBefore, absorbing clock drift between the
    // issuing instance and a (possibly trailing) validating instance under ClockSkew=Zero (ADR-0024).
    private const int ClockDriftBufferSeconds = 60;

    public async Task<JwtTokenResponse> GenerateTokenAsync(User user, bool rememberMe, string audience, CancellationToken cancellationToken = default)
    {
        // Each host's login refuses the profiles its audience does not serve, but that check is per
        // command and the mint is the one seam every issuing command crosses. A Customer holding a
        // partner-audience session is an account signed in where it has no business, so the pair is
        // refused here as an invariant: the command is expected to have refused it with its own key first.
        if (!Admits(audience, user.Profile))
        {
            throw new InvalidOperationException($"A {audience} session is never minted for a {user.Profile} account; the issuing command refuses it first.");
        }

        // Every token mint runs on an anonymous request, so the RefreshToken row added below would be
        // stamped with no tenant. The row belongs to the user being authenticated — which is also what
        // the JWT will say (ADR-0061 D4). This deliberately REPLACES the market operator the scope
        // behaviour set on a social sign-in: an existing account keeps its own operator, whichever
        // market the request named. Nothing stamped is added between the two overrides. It runs before
        // the confirmation check because the adoption is the authentication's, not the mint's: a correct
        // password on an unconfirmed address opens no session but still leaves a sign-in audit row, and
        // that row is stamped from the ambient tenant at commit (ADR-0062 D7).
        if (!string.IsNullOrEmpty(user.TenantId))
        {
            tenantProvider.SetTenantOverride(user.TenantId);
        }

        if (!user.IsEmailConfirmed)
        {
            return new JwtTokenResponse(
                Token: string.Empty,
                IsEmailConfirmed: false);
        }

        user.RecordLogin(timeProvider.GetUtcNow());

        var employeeId = await ResolveEmployeeIdAsync(user, cancellationToken);
        var accessToken = GenerateAccessToken(user, employeeId, audience, requestMetadata.DeviceId);
        var refresh = refreshTokenService.Issue(
            userId: user.Id,
            rememberMe: rememberMe,
            audience: audience,
            deviceLabel: requestMetadata.DeviceLabel,
            ipAddress: requestMetadata.IpAddress,
            deviceId: requestMetadata.DeviceId);

        return new JwtTokenResponse(
            Token: accessToken,
            IsEmailConfirmed: true,
            UserId: user.Id,
            Email: user.Email,
            RefreshToken: refresh.RawToken,
            RefreshTokenExpiresAt: refresh.Record.ExpiresAt,
            Role: user.Profile.ToString());
    }

    // The customer hosts serve every profile (a cleaner may book as a customer — Login has no gate);
    // the partner hosts serve a cleaner or an administrator; the admin host an administrator only.
    private static bool Admits(string audience, UserProfile profile) => audience switch
    {
        JwtAudiences.Customer => true,
        JwtAudiences.Partner or JwtAudiences.Mobile => profile is UserProfile.Employee or UserProfile.Administrator,
        JwtAudiences.Admin => profile == UserProfile.Administrator,
        _ => false,
    };

    private async Task<string?> ResolveEmployeeIdAsync(User user, CancellationToken cancellationToken)
    {
        if (user.Profile != UserProfile.Employee)
        {
            return null;
        }
        // Tenant-ignoring: login runs with no tenant claim yet, so the tenant-scoped read would
        // miss a tenant-stamped employee and mint a token without employee_id (T-0361).
        var employee = await employeeRepository.GetByUserEmailIgnoringTenantAsync(user.Email, cancellationToken);
        return employee?.Id;
    }

    private string GenerateAccessToken(User user, string? employeeId, string audience, string? deviceId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));

        // The whole token clock rides TimeProvider (T-0410): NotBefore/IssuedAt must share the same
        // base as Expires, otherwise a controlled clock (tests) can put Expires before the real-now
        // NotBefore and the handler rejects it. Prod uses TimeProvider.System, so values are unchanged.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = jwtSettings.Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(user.SetClaims(employeeId, deviceId)),
            // Backdate ONLY NotBefore by a small buffer. A validating instance whose clock trails the
            // issuer's would otherwise reject a just-minted token as "not yet valid" (nbf in the future)
            // on the very first authed call after login — fatal because the mobile bearer runs
            // ClockSkew=Zero (ADR-0024). Expires stays exactly now+TTL, so the access-token lifetime that
            // TC-REVOKE-TTL-2 pins as the device-revocation bound is unchanged; only the nbf window widens.
            NotBefore = now.AddSeconds(-ClockDriftBufferSeconds),
            IssuedAt = now,
            Expires = now.AddMinutes(jwtSettings.AccessTokenExpMinutes),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
