using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
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

public class Login
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
                .MustAsync((command, _, cancellationToken) => AccountIsNotLockedOut(command.Email, cancellationToken))
                .WithMessage(BusinessErrorMessage.AccountLocked)
                .WithErrorCode(nameof(Command.Password))
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

        // The lockout check precedes the password check (Cascade.Stop), so a locked account never
        // evaluates the password — no correctness oracle and no further counting while locked.
        private async Task<bool> AccountIsNotLockedOut(string email, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(email, cancellationToken);
            return user is null || !user.IsLockedOut(DateTimeOffset.UtcNow);
        }

        private async Task<bool> HasValidPassword(Command user, CancellationToken cancellationToken)
        {
            var userEntity = await userRepository.GetByEmailAsync(user.Email, cancellationToken);
            if (userEntity is null)
            {
                return false;
            }

            if (user.Password.CheckIfPasswordSame(userEntity.Password!))
            {
                return true;
            }

            await userRepository.RecordFailedLoginAsync(user.Email, DateTimeOffset.UtcNow, cancellationToken);
            return false;
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