using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Users;
using System.IdentityModel.Tokens.Jwt;
using Cleansia.Infra.Common.Configuration.Interfaces;

namespace Cleansia.Core.AppServices.Services;

public class TokenService(IJwtSettings jwtSettings) : ITokenService
{
    public JwtTokenResponse GenerateToken(User user, bool rememberMe)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(user.SetClaims()),
            Expires = DateTime.UtcNow.AddHours(rememberMe
                ? jwtSettings.CookieTokenExpHours
                : jwtSettings.DefaultTokenExpHours),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return new JwtTokenResponse(
            Token: user.IsEmailConfirmed ? tokenHandler.WriteToken(token) : string.Empty,
            IsEmailConfirmed: user.IsEmailConfirmed);
    }
}
