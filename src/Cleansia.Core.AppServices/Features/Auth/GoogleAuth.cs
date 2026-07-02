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
        public Validator()
        {
            // Identity (email, subject) and the account-type safety guard are bound from the VERIFIED
            // Google ID-token in the Handler, never from the client (S1). The validator therefore keeps
            // ONLY shape rules on the fields the handler
            // actually uses: the token (verified) and the display name (the ID-token may carry no name
            // claim). command.Email / command.GoogleId are intentionally NOT validated here — they are
            // client-supplied, the handler ignores them, and validating them gave a false sense of a
            // guard on the wrong, attacker-controlled email.
            AddFirstNameRules(command => command.FirstName);
            AddLastNameRules(command => command.LastName);

            RuleFor(command => command.Token)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Token));
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

            var user = await userRepository.GetByEmailIgnoringTenantAsync(claims.Email, cancellationToken);
            if (user is not null)
            {
                // S1: the account-type guard MUST run against the account the handler
                // actually authenticates — the VERIFIED claims.Email — not the client-supplied
                // command.Email the validator used to check. Block a Google login from binding into an
                // existing password (Internal) account that shares this verified email.
                if (user.AuthenticationType != AuthenticationType.Google)
                {
                    return BusinessResult.Failure<JwtTokenResponse>(
                        new Error(nameof(Command.Email), BusinessErrorMessage.InternalAuthTypeError));
                }

                if (!user.IsActive)
                {
                    return BusinessResult.Failure<JwtTokenResponse>(
                        new Error(nameof(Command.Email), BusinessErrorMessage.InvalidPassword));
                }
                return BusinessResult.Success(await tokenService.GenerateTokenAsync(user, rememberMe: true, hostAudience.Audience, cancellationToken));
            }

            // Provision only when Google reports the email as verified — reject an unverifiable email
            // rather than create an account around it (parity with the AppleAuth gate; the takeover
            // guard above then rests on a verified email for both providers).
            if (!claims.EmailVerified)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Token), BusinessErrorMessage.InvalidGoogleUserToken));
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
