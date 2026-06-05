using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth.Validators;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Auth;

public class GoogleAuth
{
    public class Validator : BaseAuthValidator<Command>
    {
        private readonly IUserRepository _userRepository;

        public Validator(IUserRepository userRepository)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

            AddEmailRules(command => command.Email);
            AddFirstNameRules(command => command.FirstName);
            AddLastNameRules(command => command.LastName);

            // Token verification has moved to IGoogleTokenVerifier (called by the Handler) so identity is
            // bound from the VERIFIED Google ID-token, never the client (T-0105 / IDA-SEC-01, S1). The
            // validator keeps shape rules only.
            RuleFor(command => command.Token)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Token));

            RuleFor(command => command.GoogleId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.GoogleId));

            RuleFor(user => user.Email)
                .MustAsync(UserAuthenticationTypeIsGoogle)
                .WithMessage(BusinessErrorMessage.InternalAuthTypeError)
                .WithErrorCode(nameof(Command.Email));
        }

        private async Task<bool> UserAuthenticationTypeIsGoogle(string email, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            return user is null || user.AuthenticationType == AuthenticationType.Google;
        }
    }

    public record Command(
        string Token,
        string GoogleId,
        string Email,
        string FirstName,
        string LastName)
        : ICommand<JwtTokenResponse>;

    public class Handler(
        IGoogleTokenVerifier googleTokenVerifier,
        ITokenService tokenService,
        ICartRepository cartRepository,
        IUserRepository userRepository,
        IHostAudienceProvider hostAudience)
        : ICommandHandler<Command, JwtTokenResponse>
    {
        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            // S1 server-truth-identity: verify the Google ID-token server-side and bind identity from the
            // VERIFIED claims (email + subject), never the client-supplied command.Email / command.GoogleId.
            var claims = await googleTokenVerifier.VerifyAsync(command.Token, cancellationToken);
            if (claims is null)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Token), BusinessErrorMessage.InvalidGoogleUserToken));
            }

            var user = await userRepository.GetByEmailAsync(claims.Email, cancellationToken);
            if (user is not null)
            {
                if (!user.IsActive)
                {
                    return BusinessResult.Failure<JwtTokenResponse>(
                        new Error(nameof(Command.Email), BusinessErrorMessage.InvalidPassword));
                }
                return BusinessResult.Success(await tokenService.GenerateTokenAsync(user, rememberMe: true, hostAudience.Audience, cancellationToken));
            }

            // FirstName / LastName are kept from the command — the Google ID-token may not carry a name
            // claim, so the client-provided display name is the only available source for those two.
            var userEntity = User.CreateWithGoogle(claims.Email, command.FirstName, command.LastName, claims.Subject);

            userRepository.Add(userEntity);
            cartRepository.Add(Cart.CreateWithUser(userEntity));

            return BusinessResult.Success(await tokenService.GenerateTokenAsync(userEntity, rememberMe: true, hostAudience.Audience, cancellationToken));
        }
    }
}
