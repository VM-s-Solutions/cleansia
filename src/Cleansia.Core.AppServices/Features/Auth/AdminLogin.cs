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
/// Admin-specific login command that returns a token with HasAdminAccess flag.
/// The flag indicates whether the user has Administrator or Employee role.
/// Frontend should check this flag and redirect unauthorized users.
/// </summary>
public class AdminLogin
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
        bool RememberMe)
        : ICommand<JwtTokenResponse>
    {
        public string? TrustedDeviceToken { get; init; }
    }

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

            if (user.Profile != UserProfile.Administrator)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error("AdminLogin", BusinessErrorMessage.InsufficientPrivileges));
            }

            user.ResetLoginThrottle();

            var tokenResponse = await tokenService.GenerateTokenAsync(user, command.RememberMe, hostAudience.Audience, cancellationToken);

            return BusinessResult.Success(tokenResponse with { HasAdminAccess = true });
        }
    }
}
