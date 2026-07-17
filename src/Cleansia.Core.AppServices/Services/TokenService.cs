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
    TimeProvider timeProvider)
    : ITokenService
{
    public async Task<JwtTokenResponse> GenerateTokenAsync(User user, bool rememberMe, string audience, CancellationToken cancellationToken = default)
    {
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
            NotBefore = now,
            IssuedAt = now,
            Expires = now.AddMinutes(jwtSettings.AccessTokenExpMinutes),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
