using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
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

    public record Command(string Token, UserProfile? RequiredProfile = null, string? RequiredAudience = null) : ICommand<JwtTokenResponse>;

    internal class Handler(
        IRefreshTokenService refreshTokenService,
        IUserRepository userRepository,
        IEmployeeRepository employeeRepository,
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

            if (!string.IsNullOrEmpty(command.RequiredAudience)
                && issued.Record.Audience != command.RequiredAudience)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Token), BusinessErrorMessage.InvalidRefreshToken));
            }

            var user = await userRepository.GetByIdAsync(issued.Record.UserId, cancellationToken);
            if (user is null || !user.IsActive)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Token), BusinessErrorMessage.InvalidRefreshToken));
            }

            if (command.RequiredProfile.HasValue && user.Profile != command.RequiredProfile.Value)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Token), BusinessErrorMessage.InvalidRefreshToken));
            }

            string? employeeId = null;
            if (user.Profile == UserProfile.Employee)
            {
                var employee = await employeeRepository.GetByUserEmailAsync(user.Email, cancellationToken);
                employeeId = employee?.Id;
            }

            var audience = issued.Record.Audience ?? string.Empty;
            var accessToken = GenerateAccessToken(user, employeeId, audience, jwtSettings);

            return BusinessResult.Success(new JwtTokenResponse(
                Token: accessToken,
                IsEmailConfirmed: user.IsEmailConfirmed,
                UserId: user.Id,
                Email: user.Email,
                RefreshToken: issued.RawToken,
                RefreshTokenExpiresAt: issued.Record.ExpiresAt,
                Role: user.Profile.ToString()));
        }

        private static string GenerateAccessToken(User user, string? employeeId, string audience, IJwtSettings jwtSettings)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = jwtSettings.Issuer,
                Audience = audience,
                Subject = new ClaimsIdentity(user.SetClaims(employeeId)),
                Expires = DateTime.UtcNow.AddMinutes(jwtSettings.AccessTokenExpMinutes),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
            };
            var token = tokenHandler.CreateToken(descriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
