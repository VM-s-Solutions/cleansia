using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using System.Text.Json.Serialization;

namespace Cleansia.Core.AppServices.Features.Auth;

public class PartnerLogin
{
    public class Validator : LoginValidator<Command>
    {
        public Validator(
            IUserRepository userRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IRefreshTokenService refreshTokenService,
            IAuditContext auditContext)
            : base(userRepository, refreshTokenRepository, refreshTokenService, auditContext,
                c => c.Email, c => c.Password, c => c.RememberMe, c => c.TrustedDeviceToken)
        {
        }
    }

    public record Command(
        string Email,
        string Password,
        bool RememberMe)
        : ICommand<JwtTokenResponse>
    {
        // Web hosts derive the trusted-device marker from the HttpOnly refresh cookie server-side
        // (the body never carries it), so JsonIgnore keeps it off the wire. The mobile partner login
        // path (MobilePartnerLogin) carries it in the body instead.
        [JsonIgnore]
        public string? TrustedDeviceToken { get; init; }
    }

    internal class Handler(
        ITokenService tokenService,
        IUserRepository userRepository,
        IHostAudienceProvider hostAudience,
        ICompanySignInGate companySignInGate)
        : ICommandHandler<Command, JwtTokenResponse>
    {
        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailIgnoringTenantAsync(command.Email, cancellationToken);

            if (user is null || !user.IsActive)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Email), BusinessErrorMessage.InvalidPassword));
            }

            if (user.Profile != UserProfile.Employee && user.Profile != UserProfile.Administrator)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(command.Email), BusinessErrorMessage.InsufficientPrivileges));
            }

            if (await companySignInGate.RefusalForAsync(user, hostAudience.Audience, cancellationToken) is { } refusal)
            {
                return BusinessResult.Failure<JwtTokenResponse>(new Error(nameof(command.Email), refusal));
            }

            user.ResetLoginThrottle();

            return BusinessResult.Success(await tokenService.GenerateTokenAsync(user, command.RememberMe, hostAudience.Audience, cancellationToken));
        }
    }
}
