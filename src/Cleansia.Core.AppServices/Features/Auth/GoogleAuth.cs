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
using Microsoft.EntityFrameworkCore;

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

    // TermsAccepted is what tells a signup apart from a sign-in: only the signup screen carries the terms
    // tick, and only a call that asserts it may bring an account into existence. It is client-asserted
    // and deliberately so — a checkbox is not a fact the server can observe — so it is NOT an
    // authorization control; it is the record of which screen the account was created from. It defaults
    // to false so a caller that says nothing gets the sign-in-only behaviour.
    public record Command(
        string Token,
        string GoogleId,
        string Email,
        string FirstName,
        string LastName,
        bool TermsAccepted = false)
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

            // The Google subject is the account's stable identity; the email is a provider-owned
            // attribute the user can change at will. Resolving by subject first means changing the
            // address on a Google account no longer orphans the row and provisions a duplicate.
            var user = await userRepository.GetByGoogleIdIgnoringTenantAsync(claims.Subject, cancellationToken);

            // S1: only an email GOOGLE vouched for may resolve an EXISTING account. Without this gate a
            // token asserting an address Google never verified — the same address a victim registered
            // under — resolves onto their row and is handed their JWT. Provisioning is gated on the same
            // claim below, so an unverified email has no path at all (parity with AppleAuth).
            // Reached only by accounts provisioned before subjects were stored; once one signs in here
            // the subject is bound below and it never takes this path again.
            if (user is null && claims.EmailVerified && !string.IsNullOrWhiteSpace(claims.Email))
            {
                user = await userRepository.GetByEmailIgnoringTenantAsync(claims.Email, cancellationToken);
            }

            if (user is not null)
            {
                // S1: the account-type guard MUST run against the account the handler
                // actually authenticates — the VERIFIED claims.Email — not the client-supplied
                // command.Email the validator used to check. Block a Google login from binding into an
                // existing password (Internal) account that shares this verified email.
                // The rejection names the provider the colliding account ACTUALLY uses (the same switch
                // the password login uses) — telling an Apple user to "sign in with your email and
                // password" sends them to a dead end. No extra disclosure: the caller already holds a
                // token Google minted for this verified identity.
                if (user.AuthenticationType != AuthenticationType.Google)
                {
                    return BusinessResult.Failure<JwtTokenResponse>(
                        new Error(nameof(Command.Email), AuthTypeErrorMessages.For(user.AuthenticationType)));
                }

                if (!user.IsActive)
                {
                    return BusinessResult.Failure<JwtTokenResponse>(
                        new Error(nameof(Command.Email), BusinessErrorMessage.InvalidPassword));
                }

                // Anchor the account to the subject on the one sign-in that resolved by email. A no-op
                // when the subject lookup is what found this row. Never overwrites a bound subject —
                // that rule is the S1 property, enforced in LinkGoogleId itself rather than here, so it
                // holds for every caller. The write rides the UnitOfWork commit; the row is tracked.
                user.LinkGoogleId(claims.Subject);

                return BusinessResult.Success(await tokenService.GenerateTokenAsync(user, rememberMe: true, hostAudience.Audience, cancellationToken));
            }

            // Provision only when Google reports the email as verified — reject an unverifiable email
            // rather than create an account around it (parity with the AppleAuth gate). This is where an
            // unverified token lands: the lookup above declined to resolve it onto an existing account, so
            // failing closed here is the only remaining outcome.
            if (!claims.EmailVerified)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Token), BusinessErrorMessage.InvalidGoogleUserToken));
            }

            if (!command.TermsAccepted)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.TermsAccepted), BusinessErrorMessage.SocialAccountNotFound));
            }

            // FirstName / LastName are kept from the command — the Google ID-token may not carry a name
            // claim, so the client-provided display name is the only available source for those two.
            var userEntity = User.CreateWithGoogle(claims.Email, command.FirstName, command.LastName, claims.Subject);

            userRepository.Add(userEntity);
            cartRepository.Add(Cart.CreateWithUser(userEntity));

            // The resolve-by-email fallback above and this insert cross a snapshot boundary with no
            // lock, so (TenantId, Email) UNIQUE is what actually arbitrates two simultaneous
            // provisionings of the same verified address (ADR-0050). FLUSH here and own the loser's
            // 23505 — and do it BEFORE minting a JWT, so no token is issued for a row that was rejected.
            try
            {
                await userRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
                when (DbConstraintViolation.IsUniqueViolationOn(ex, DbConstraintNames.UsersTenantIdEmailUnique))
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Email), BusinessErrorMessage.ExistingUserWithEmail));
            }

            return BusinessResult.Success(await tokenService.GenerateTokenAsync(userEntity, rememberMe: true, hostAudience.Audience, cancellationToken));
        }
    }
}
