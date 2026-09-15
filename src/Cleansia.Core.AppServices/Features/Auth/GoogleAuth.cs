using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Auth;

/// <summary>
/// Sign-in-or-register on a customer host; sign-in only anywhere else. The marker records the sign-in of
/// an existing account as a session act; the provisioning branch declines the row, because a
/// registration's proof is the two consent rows it writes and a login row would say a session was
/// opened by an account that did not exist a moment ago. A refusal on either branch is recorded — a bad
/// token or a sign-in with no account is exactly the login history the row exists for.
///
/// <para>The partner hosts route this command for their cleaners, and a host that is not a customer
/// host provisions nothing: a cleaner's account is opened through <c>RegisterEmployee</c>, and a
/// freshly provisioned Customer minted a partner-audience token would be an account that can sign in
/// where it has no business. Whom such a host signs in is <see cref="PartnerLogin"/>'s rule — an
/// Employee or an Administrator, never a Customer.</para>
/// </summary>
[AuditAction("customer.session.login", Audience = AuditAudience.Customer, ResourceType = "User", AllowsAnonymousActor = true)]
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
        bool TermsAccepted = false,
        // The market a first sign-in provisions into; null is the default market (ADR-0061 D3). An
        // existing account keeps its own operator: TokenService re-scopes to it before the token is
        // minted.
        string? CountryId = null)
        : ICommand<JwtTokenResponse>, IOperatorScopedRequest;

    public class Handler(
        IGoogleTokenVerifier googleTokenVerifier,
        ITokenService tokenService,
        ICartRepository cartRepository,
        IUserRepository userRepository,
        IHostAudienceProvider hostAudience,
        IConsentService consentService,
        ILegalDocumentResolver legalDocumentResolver,
        IAuditContext auditContext)
        : ICommandHandler<Command, JwtTokenResponse>
    {
        private bool IsCustomerHost => hostAudience.Audience == JwtAudiences.Customer;

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
                // A refusal below is this account's row, not the IP's alone.
                auditContext.RecordEvidence("User", user.Id, payload: null, actorUserId: user.Id);

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

                if (!IsCustomerHost && user.Profile is not (UserProfile.Employee or UserProfile.Administrator))
                {
                    return BusinessResult.Failure<JwtTokenResponse>(
                        new Error(nameof(Command.Email), BusinessErrorMessage.InsufficientPrivileges));
                }

                // Anchor the account to the subject on the one sign-in that resolved by email. A no-op
                // when the subject lookup is what found this row. Never overwrites a bound subject —
                // that rule is the S1 property, enforced in LinkGoogleId itself rather than here, so it
                // holds for every caller. The write rides the UnitOfWork commit; the row is tracked.
                user.LinkGoogleId(claims.Subject);

                var session = await tokenService.GenerateTokenAsync(user, rememberMe: true, hostAudience.Audience, cancellationToken);

                auditContext.RecordEvidence(
                    "User",
                    user.Id,
                    new LoginEvidence(LoginEvidence.GoogleMethod, RememberMe: true, hostAudience.Audience, session.IsEmailConfirmed),
                    actorUserId: user.Id);

                return BusinessResult.Success(session);
            }

            if (!IsCustomerHost)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Email), BusinessErrorMessage.SocialAccountNotFound));
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

            auditContext.DeclineSuccessRow();

            // FirstName / LastName are kept from the command — the Google ID-token may not carry a name
            // claim, so the client-provided display name is the only available source for those two.
            var userEntity = User.CreateWithGoogle(claims.Email, command.FirstName, command.LastName, claims.Subject);

            userRepository.Add(userEntity);
            cartRepository.Add(Cart.CreateWithUser(userEntity));

            // Reached only with the tick asserted. These two rows are the registration proof (the
            // session row is declined above), so the consent rides the same flush as the account.
            await consentService.TryGrantAsync(userEntity.Id, ConsentType.TermsOfService,
                await legalDocumentResolver.ResolveInForceAsync(LegalDocumentType.TermsOfService, command.CountryId, cancellationToken), cancellationToken);
            await consentService.TryGrantAsync(userEntity.Id, ConsentType.PrivacyPolicy,
                await legalDocumentResolver.ResolveInForceAsync(LegalDocumentType.PrivacyPolicy, command.CountryId, cancellationToken), cancellationToken);

            // The resolve-by-email fallback above and this insert cross a snapshot boundary with no
            // lock, so the global Email UNIQUE index is what actually arbitrates two simultaneous
            // provisionings of the same verified address (ADR-0050 D2). FLUSH here and own the loser's
            // 23505 — and do it BEFORE minting a JWT, so no token is issued for a row that was rejected.
            try
            {
                await userRepository.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
                when (DbConstraintViolation.IsUniqueViolation(ex))
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Email), BusinessErrorMessage.ExistingUserWithEmail));
            }

            return BusinessResult.Success(await tokenService.GenerateTokenAsync(userEntity, rememberMe: true, hostAudience.Audience, cancellationToken));
        }
    }
}
