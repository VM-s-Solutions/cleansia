using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth.Validators;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Auth;

public class Login
{
    public class Validator : LoginValidator<Command>
    {
        public Validator(IUserRepository userRepository)
            : base(userRepository, c => c.Email, c => c.Password, c => c.RememberMe)
        {
        }
    }

    public record Command(
        string Email,
        string Password,
        bool RememberMe)
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

            user.ResetLoginThrottle();

            return BusinessResult.Success(await tokenService.GenerateTokenAsync(user, command.RememberMe, hostAudience.Audience, cancellationToken));
        }
    }
}