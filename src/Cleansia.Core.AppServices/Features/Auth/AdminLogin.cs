using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Features.Auth.Validators;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Auth;

/// <summary>
/// Admin-specific login command that returns a token with HasAdminAccess flag.
/// The flag indicates whether the user has Administrator or Employee role.
/// Frontend should check this flag and redirect unauthorized users.
/// </summary>
public class AdminLogin
{
    public class Validator : BaseAuthValidator<Command>
    {
        private readonly IUserRepository userRepository;

        public Validator(IUserRepository userRepository)
        {
            this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

            AddEmailRules(command => command.Email);

            RuleFor(command => command.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync((command, _, cancellationToken) => HasValidPassword(command, cancellationToken))
                .WithMessage(BusinessErrorMessage.InvalidPassword)
                .WithErrorCode(nameof(Command.Password))
                .WhenAsync((command, cancellationToken) => UserAuthenticationTypeIsInternal(command.Email, cancellationToken));

            RuleFor(user => user.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(userRepository.ExistsWithEmailAsync)
                .WithMessage(BusinessErrorMessage.NotExistingUserWithEmail)
                .WithErrorCode(nameof(Command.Email))
                .MustAsync(UserAuthenticationTypeIsInternal)
                .WithMessage(BusinessErrorMessage.GoogleAuthTypeError)
                .WithErrorCode(nameof(Command.Email));

            RuleFor(command => command.RememberMe)
                .NotNull()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.RememberMe));
        }

        private async Task<bool> UserAuthenticationTypeIsInternal(string email, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(email, cancellationToken);
            return user is not null && user.AuthenticationType == AuthenticationType.Internal;
        }

        private async Task<bool> HasValidPassword(Command user, CancellationToken cancellationToken)
        {
            var userEntity = await userRepository.GetByEmailAsync(user.Email, cancellationToken);
            return userEntity is not null && user.Password.CheckIfPasswordSame(userEntity.Password!);
        }
    }

    public record Command(
        string Email,
        string Password,
        bool RememberMe)
        : ICommand<JwtTokenResponse>;

    internal class Handler(
        ITokenService tokenService,
        IUserRepository userRepository)
        : ICommandHandler<Command, JwtTokenResponse>
    {
        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(command.Email, cancellationToken);

            var tokenResponse = tokenService.GenerateToken(user!, command.RememberMe);

            // Check if user has admin privileges (Administrator or Employee role)
            var hasAdminAccess = user!.Profile == UserProfile.Administrator;

            return BusinessResult.Success(tokenResponse with { HasAdminAccess = hasAdminAccess });
        }
    }
}
