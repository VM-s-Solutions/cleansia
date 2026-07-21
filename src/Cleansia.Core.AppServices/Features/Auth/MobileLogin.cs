using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth.Validators;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Auth;

/// <summary>
/// Customer mobile login. Identical behavior to <see cref="Login"/> (permissive for any active
/// profile), but the native clients can't read the HttpOnly refresh cookie the web hosts use, so the
/// trusted-device lockout-bypass marker is carried in the request body instead. The web
/// <see cref="Login"/> command keeps that field off the wire.
/// </summary>
public class MobileLogin
{
    public class Validator : LoginValidator<Command>
    {
        public Validator(
            IUserRepository userRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IRefreshTokenService refreshTokenService)
            : base(userRepository, refreshTokenRepository, refreshTokenService,
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

            user.ResetLoginThrottle();

            // Native customer app: issue a long-lived (RefreshTokenExpDays) refresh token regardless of
            // the client's rememberMe toggle. A phone is a personal device and users expect a persistent
            // session (Wolt/Bolt-style) — the short 1-day token would sign them out after a day of not
            // opening the app. The refresh expiry slides on every rotation, so the session survives as
            // long as the app refreshes within the window. Parity with the social (Apple/Google) paths,
            // which already force the long lifetime.
            return BusinessResult.Success(await tokenService.GenerateTokenAsync(user, rememberMe: true, hostAudience.Audience, cancellationToken));
        }
    }
}
