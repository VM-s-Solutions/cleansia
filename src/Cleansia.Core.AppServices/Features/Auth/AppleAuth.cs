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
            // raw nonce (both verified server-side). The display name is optional here — Apple returns a
            // name ONLY on the first authorization, may omit the family name, and sends no name at all on
            // every later sign-in, so requiring it would reject legitimate logins. When a value is present
            // it is only length-capped; identity never depends on it.
            RuleFor(command => command.FirstName)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .WithErrorCode(nameof(Command.FirstName));

            RuleFor(command => command.LastName)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .WithErrorCode(nameof(Command.LastName));

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
        string? FirstName,
        string? LastName)
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

            // The display name is taken from the command only for provisioning — Apple returns a name
            // claim only on the first authorization, so the client-provided value is the only source, and
            // it may be absent or partial (see ResolveDisplayName). Identity is the verified claims.
            var (firstName, lastName) = ResolveDisplayName(command.FirstName, command.LastName);
            var userEntity = User.CreateWithApple(claims.Email, firstName, lastName, claims.Subject);

            userRepository.Add(userEntity);
            cartRepository.Add(Cart.CreateWithUser(userEntity));

            return BusinessResult.Success(await tokenService.GenerateTokenAsync(userEntity, rememberMe: true, hostAudience.Audience, cancellationToken));
        }

        // Apple hands the name only on the first authorization and may omit the family name entirely, or
        // fold the whole name into a single field. Take whatever it gives: an empty last name is allowed;
        // when only a space-separated full name arrives, split off the first token as the given name so the
        // family name isn't needlessly left blank.
        private static (string FirstName, string LastName) ResolveDisplayName(string? firstName, string? lastName)
        {
            var first = (firstName ?? string.Empty).Trim();
            var last = (lastName ?? string.Empty).Trim();

            if (last.Length == 0 && first.Contains(' '))
            {
                var separatorIndex = first.IndexOf(' ');
                last = first[(separatorIndex + 1)..].Trim();
                first = first[..separatorIndex];
            }

            return (first, last);
        }
    }
}
