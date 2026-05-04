using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using FluentValidation;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Auth;

/// <summary>
/// Rotates a refresh token. Called when the access token expires (15 min).
/// Anonymous endpoint — the refresh token IS the credential.
/// </summary>
public class RefreshToken
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Token)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Token));
        }
    }

    public record Command(string Token) : ICommand<JwtTokenResponse>;

    internal class Handler(
        IRefreshTokenService refreshTokenService,
        IUserRepository userRepository,
        IRequestMetadataProvider requestMetadata,
        IJwtSettings jwtSettings)
        : ICommandHandler<Command, JwtTokenResponse>
    {
        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            IssuedRefreshToken issued;
            try
            {
                issued = await refreshTokenService.RotateAsync(
                    command.Token,
                    deviceLabel: requestMetadata.DeviceLabel,
                    ipAddress: requestMetadata.IpAddress,
                    cancellationToken);
            }
            catch (RefreshTokenValidationException ex)
            {
                var errorKey = ex.IsTheftSignal
                    ? BusinessErrorMessage.RefreshTokenReused
                    : BusinessErrorMessage.InvalidRefreshToken;
                return BusinessResult.Failure<JwtTokenResponse>(new Error(nameof(Command.Token), errorKey));
            }

            var user = await userRepository.GetByIdAsync(issued.Record.UserId, cancellationToken);
            if (user is null || !user.IsActive)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Token), BusinessErrorMessage.InvalidRefreshToken));
            }

            var accessToken = GenerateAccessToken(user, jwtSettings);

            return BusinessResult.Success(new JwtTokenResponse(
                Token: accessToken,
                IsEmailConfirmed: user.IsEmailConfirmed,
                UserId: user.Id,
                Email: user.Email,
                RefreshToken: issued.RawToken,
                RefreshTokenExpiresAt: issued.Record.ExpiresAt));
        }

        private static string GenerateAccessToken(User user, IJwtSettings jwtSettings)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(user.SetClaims()),
                Expires = DateTime.UtcNow.AddMinutes(jwtSettings.AccessTokenExpMinutes),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
            };
            var token = tokenHandler.CreateToken(descriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
