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
using System.Text.Json.Serialization;

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

    // RequiredProfile/RequiredAudience are the host's per-host refresh pin (ADR-0001). They are
    // server-authoritative: each AuthController sets them from its own host identity and a
    // client-sent value would be discarded. JsonIgnore keeps them off the wire so they never appear
    // in a generated client and can never be supplied by a caller — only Token crosses the wire.
    public record Command(string Token) : ICommand<JwtTokenResponse>
    {
        [JsonIgnore]
        public UserProfile? RequiredProfile { get; init; }

        [JsonIgnore]
        public string? RequiredAudience { get; init; }
    }

    internal class Handler(
        IRefreshTokenService refreshTokenService,
        IUserRepository userRepository,
        IEmployeeRepository employeeRepository,
        IRequestMetadataProvider requestMetadata,
        IJwtSettings jwtSettings,
        TimeProvider timeProvider)
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
                    cancellationToken,
                    deviceId: requestMetadata.DeviceId);
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

            // The refresh request is anonymous (the refresh token is the credential), so the tenant
            // filter would hide a tenant-stamped user; the rotated token's own UserId is the scope.
            var user = await userRepository.GetByIdIgnoringTenantAsync(issued.Record.UserId, cancellationToken);
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

            // Persist the rotation only now that every accept/reject gate has passed — a rejected
            // refresh must not rotate. This flush is where the fail-closed concurrency check fires: a
            // revoke that raced this rotation makes it throw, so the new token never escapes revocation.
            try
            {
                await refreshTokenService.CommitRotationAsync(cancellationToken);
            }
            catch (RefreshTokenValidationException)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Token), BusinessErrorMessage.InvalidRefreshToken));
            }

            string? employeeId = null;
            if (user.Profile == UserProfile.Employee)
            {
                // Tenant-ignoring: refresh runs with no tenant claim, so the tenant-scoped read would
                // miss a tenant-stamped employee and mint a token without employee_id (T-0361).
                var employee = await employeeRepository.GetByUserEmailIgnoringTenantAsync(user.Email, cancellationToken);
                employeeId = employee?.Id;
            }

            var audience = issued.Record.Audience ?? string.Empty;
            var accessToken = GenerateAccessToken(user, employeeId, audience, issued.Record.DeviceId, jwtSettings, timeProvider);

            return BusinessResult.Success(new JwtTokenResponse(
                Token: accessToken,
                IsEmailConfirmed: user.IsEmailConfirmed,
                UserId: user.Id,
                Email: user.Email,
                RefreshToken: issued.RawToken,
                RefreshTokenExpiresAt: issued.Record.ExpiresAt,
                Role: user.Profile.ToString()));
        }

        private static string GenerateAccessToken(User user, string? employeeId, string audience, string? deviceId, IJwtSettings jwtSettings, TimeProvider timeProvider)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));
            // Whole token clock on TimeProvider (T-0410) — NotBefore/IssuedAt share Expires' base.
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = jwtSettings.Issuer,
                Audience = audience,
                Subject = new ClaimsIdentity(user.SetClaims(employeeId, deviceId)),
                NotBefore = now,
                IssuedAt = now,
                Expires = now.AddMinutes(jwtSettings.AccessTokenExpMinutes),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
            };
            var token = tokenHandler.CreateToken(descriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
