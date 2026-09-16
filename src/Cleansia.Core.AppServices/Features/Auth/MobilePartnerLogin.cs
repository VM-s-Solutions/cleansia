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
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Auth;

/// <summary>
/// Partner mobile login. Identical behavior to <see cref="PartnerLogin"/> (Employee/Administrator
/// only), but the native clients can't read the HttpOnly refresh cookie the web hosts use, so the
/// trusted-device lockout-bypass marker is carried in the request body instead. The web
/// <see cref="PartnerLogin"/> command keeps that field off the wire.
/// </summary>
public class MobilePartnerLogin
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
        bool RememberMe,
        string? TrustedDeviceToken = null)
        : ICommand<JwtTokenResponse>;

    internal class Handler(
        ITokenService tokenService,
        IUserRepository userRepository,
        IHostAudienceProvider hostAudience,
        IRequestMetadataProvider requestMetadata,
        ICompanySignInGate companySignInGate,
        ILogger<Handler> logger)
        : ICommandHandler<Command, JwtTokenResponse>
    {
        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Evidence gate for a future required-header login validator: mobile hosts should always
            // carry X-Device-Id so a session stays device-revocable. Not yet enforced — claim-less
            // tokens pass the device directory by design during transition — so we only warn (host +
            // audience, never the subject's email/PII, per S6) to measure how many logins lack it.
            if (string.IsNullOrWhiteSpace(requestMetadata.DeviceId))
            {
                logger.LogWarning(
                    "Mobile login without an X-Device-Id header on audience {Audience}.",
                    hostAudience.Audience);
            }

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
