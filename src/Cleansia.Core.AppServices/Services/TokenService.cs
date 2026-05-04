using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Users;
using System.IdentityModel.Tokens.Jwt;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;

namespace Cleansia.Core.AppServices.Services;

public class TokenService(
    IJwtSettings jwtSettings,
    IRefreshTokenService refreshTokenService,
    IRequestMetadataProvider requestMetadata)
    : ITokenService
{
    public JwtTokenResponse GenerateToken(User user, bool rememberMe)
    {
        // Unconfirmed users get an empty response — frontend routes to the email-verify flow.
        // Don't issue either access or refresh token; user can't do anything meaningful yet.
        if (!user.IsEmailConfirmed)
        {
            return new JwtTokenResponse(
                Token: string.Empty,
                IsEmailConfirmed: false);
        }

        var accessToken = GenerateAccessToken(user);
        var refresh = refreshTokenService.Issue(
            userId: user.Id,
            rememberMe: rememberMe,
            deviceLabel: requestMetadata.DeviceLabel,
            ipAddress: requestMetadata.IpAddress);

        return new JwtTokenResponse(
            Token: accessToken,
            IsEmailConfirmed: true,
            UserId: user.Id,
            Email: user.Email,
            RefreshToken: refresh.RawToken,
            RefreshTokenExpiresAt: refresh.Record.ExpiresAt);
    }

    private string GenerateAccessToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(user.SetClaims()),
            Expires = DateTime.UtcNow.AddMinutes(jwtSettings.AccessTokenExpMinutes),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
