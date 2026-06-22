using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth.Validators;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

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
        IHostAudienceProvider hostAudience)
        : ICommandHandler<Command, JwtTokenResponse>
    {
        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);

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

            user.ResetLoginThrottle();

            return BusinessResult.Success(await tokenService.GenerateTokenAsync(user, command.RememberMe, hostAudience.Audience, cancellationToken));
        }
    }
}
