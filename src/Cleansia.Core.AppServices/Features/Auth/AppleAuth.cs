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

public class AppleAuth
{
    public class Validator : BaseAuthValidator<Command>
    {
        public Validator()
        {
            // Identity (email, subject, email_verified) and the account-type safety guard are bound from
            // the VERIFIED Apple identity token in the Handler, never from the client (S1). The validator
            // therefore keeps ONLY shape rules on the fields the handler actually uses: the token and the
            // raw nonce (both verified server-side) and the first-login display name (Apple returns a name
            // only on the first authorization, so the client-provided value is the only available source).
            AddFirstNameRules(command => command.FirstName);
            AddLastNameRules(command => command.LastName);

            RuleFor(command => command.IdentityToken)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.IdentityToken));

            RuleFor(command => command.RawNonce)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.RawNonce));
        }
    }

    public record Command(
        string IdentityToken,
        string RawNonce,
        string FirstName,
        string LastName)
        : ICommand<JwtTokenResponse>;

    public class Handler(
        IAppleTokenVerifier appleTokenVerifier,
        ITokenService tokenService,
        ICartRepository cartRepository,
        IUserRepository userRepository,
        IHostAudienceProvider hostAudience)
        : ICommandHandler<Command, JwtTokenResponse>
    {
        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            // S1 server-truth-identity: verify the Apple identity token server-side (RS256/JWKS, aud/iss/
            // lifetime, and the SHA256(rawNonce)==nonce binding) and bind identity from the VERIFIED claims
            // (email + subject + email_verified), never the client-supplied request fields.
            var claims = await appleTokenVerifier.VerifyAsync(command.IdentityToken, command.RawNonce, cancellationToken);
            if (claims is null)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.IdentityToken), BusinessErrorMessage.InvalidAppleUserToken));
            }

            var user = await userRepository.GetByEmailIgnoringTenantAsync(claims.Email, cancellationToken);
            if (user is not null)
            {
                // S1: the account-type guard MUST run against the account the handler actually
                // authenticates — the VERIFIED claims.Email — not a client-supplied field. Block an Apple
                // login from binding into an existing password (Internal) OR Google account that shares
                // this verified email (the verified-email-collision takeover Google's hardening closed).
                if (user.AuthenticationType != AuthenticationType.Apple)
                {
                    return BusinessResult.Failure<JwtTokenResponse>(
                        new Error(nameof(Command.IdentityToken), BusinessErrorMessage.InternalAuthTypeError));
                }

                if (!user.IsActive)
                {
                    return BusinessResult.Failure<JwtTokenResponse>(
                        new Error(nameof(Command.IdentityToken), BusinessErrorMessage.InvalidPassword));
                }
                return BusinessResult.Success(await tokenService.GenerateTokenAsync(user, rememberMe: true, hostAudience.Audience, cancellationToken));
            }

            // Provision only when Apple reports the email as verified — reject an unverifiable email rather
            // than create an account around it.
            if (!claims.EmailVerified)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.IdentityToken), BusinessErrorMessage.InvalidAppleUserToken));
            }

            // FirstName / LastName are kept from the command — Apple returns a name claim only on the first
            // authorization, so the client-provided display name is the only available source for those two.
            var userEntity = User.CreateWithApple(claims.Email, command.FirstName, command.LastName, claims.Subject);

            userRepository.Add(userEntity);
            cartRepository.Add(Cart.CreateWithUser(userEntity));

            return BusinessResult.Success(await tokenService.GenerateTokenAsync(userEntity, rememberMe: true, hostAudience.Audience, cancellationToken));
        }
    }
}
