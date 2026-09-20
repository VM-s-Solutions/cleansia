using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Common.Validators.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Auth;

[AuditAction("customer.account.register", Audience = AuditAudience.Customer, ResourceType = "User", AllowsAnonymousActor = true)]
public class Register
{
    public class Validator : BaseAuthValidator<Command>
    {
        private readonly IUserRepository _userRepository;
        private readonly ITenantProvider _tenantProvider;

        public Validator(
            IUserRepository userRepository,
            ILanguageRepository languageRepository,
            ITenantProvider tenantProvider)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _tenantProvider = tenantProvider;

            AddEmailRules(command => command.Email);
            AddFirstNameRules(command => command.FirstName);
            AddLastNameRules(command => command.LastName);
            AddPasswordRules(command => command.Password);

            RuleFor(user => user.Email)
                .MustAsync(UserWithEmailNotExistsAsync)
                .WithMessage(BusinessErrorMessage.ExistingUserWithEmail)
                .WithErrorCode(nameof(Command.Email));

            RuleFor(user => user.Language)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Language))
                .SetValidator(new LanguageValidator(languageRepository));

            // Null and false are one refusal: a client that says nothing has not consented either.
            RuleFor(user => user.TermsAccepted)
                .Must(accepted => accepted == true)
                .WithMessage(BusinessErrorMessage.TermsNotAccepted)
                .WithErrorCode(nameof(Command.TermsAccepted));
        }

        // One identity per email across the holding (ADR-0061 D5.1): the pre-check ignores the tenant
        // so the refusal is a 400 before the flush rather than a mapped 23505 after it. An unconfirmed
        // row may be re-registered (the handler refreshes its code) only in the market this request
        // was scoped to — a visitor never silently re-registers into a company they did not choose.
        private async Task<bool> UserWithEmailNotExistsAsync(string email, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailIgnoringTenantAsync(email, cancellationToken);
            return user is null
                || (!user.IsEmailConfirmed && user.TenantId == _tenantProvider.GetCurrentTenantId());
        }
    }

    public record Command(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string Language,
        // Optional referral code entered by the customer at signup. Empty
        // when the user signed up directly. Validated + accepted server-side
        // after the user is created — bad codes do NOT block registration.
        string? ReferralCode = null,
        // The market the visitor registers with; null is the default market (ADR-0061 D3).
        string? CountryId = null,
        // The terms tick as the client asserted it; the validator refuses the registration unless it is
        // true (ADR-0062 D4 as amended 2026-09-14). Nullable so the wire contract every client was
        // built against is unchanged — null is refused, not unbindable.
        bool? TermsAccepted = null)
        : ICommand, IOperatorScopedRequest;

    public record RegistrationEvidence(
        string Method,
        string Language,
        bool ReferralCodePresent,
        bool? TermsAccepted,
        string? TermsVersion,
        string? PrivacyVersion) : ICustomerAuditPayload;

    public class Handler(
        IUserRepository userRepository,
        IReferralService referralService,
        IPendingDispatch pending,
        IConsentService consentService,
        ILegalDocumentResolver legalDocumentResolver,
        IAuditContext auditContext,
        ILogger<Handler> logger)
        : ICommandHandler<Command>
    {
        private const string EmailMethod = "Email";

        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
        {
            // Same resolution the validator proved: a non-null row here is unconfirmed and in this market.
            var userEntity = await userRepository.GetByEmailIgnoringTenantAsync(command.Email, cancellationToken);
            // Email the RAW token; the entity persists only its hash. The raw is
            // surfaced by CreateWithPassword (transient RawConfirmationToken) and returned by
            // UpdateConfirmationCode — never read back off the persisted (hashed) column.
            string rawConfirmationToken;
            if (userEntity is null)
            {
                userEntity = User.CreateWithPassword(command.Email, command.Password, command.FirstName, command.LastName, UserProfile.Customer, command.Language);
                rawConfirmationToken = userEntity.RawConfirmationToken!;
                userRepository.Add(userEntity);

                // The validator's pre-check and this insert cross a snapshot boundary with no lock, so
                // the global Email UNIQUE index is what actually arbitrates two simultaneous registrations
                // (ADR-0050 D2, ADR-0061 D5.1). FLUSH here and own the loser's 23505: the pipeline commit
                // runs after this handler returns, where the same violation can only surface as a 500 (S7b).
                try
                {
                    await userRepository.CommitAsync(cancellationToken);
                }
                catch (DbUpdateException ex)
                    when (DbConstraintViolation.IsUniqueViolation(ex))
                {
                    return BusinessResult.Failure(
                        new Error(nameof(Command.Email), BusinessErrorMessage.ExistingUserWithEmail));
                }
            }
            else
            {
                // Re-registration: user exists but hasn't confirmed — refresh the code
                rawConfirmationToken = userEntity.UpdateConfirmationCode();
            }

            // The texts in force for the market the visitor registers with — recorded whether or not the
            // box was ticked, because they are what the screen showed.
            var terms = await legalDocumentResolver.ResolveInForceAsync(LegalDocumentType.TermsOfService, command.CountryId, cancellationToken);
            var privacy = await legalDocumentResolver.ResolveInForceAsync(LegalDocumentType.PrivacyPolicy, command.CountryId, cancellationToken);

            if (command.TermsAccepted == true)
            {
                await consentService.TryGrantAsync(userEntity.Id, ConsentType.TermsOfService, terms, cancellationToken);
                await consentService.TryGrantAsync(userEntity.Id, ConsentType.PrivacyPolicy, privacy, cancellationToken);
            }

            auditContext.RecordEvidence(
                "User",
                userEntity.Id,
                new RegistrationEvidence(
                    EmailMethod,
                    command.Language,
                    !string.IsNullOrWhiteSpace(command.ReferralCode),
                    command.TermsAccepted,
                    terms?.Version,
                    privacy?.Version),
                actorUserId: userEntity.Id);

            var userName = $"{userEntity.FirstName} {userEntity.LastName}";

            EmailDispatch.EnqueueConfirmation(pending, userEntity, userName, rawConfirmationToken, command.Language);

            // Referral acceptance is fail-soft: a bad code (typo, expired,
            // self-referral) must NOT block account creation. The user can
            // re-enter at first booking via CreateOrder's late-acceptance.
            if (!string.IsNullOrWhiteSpace(command.ReferralCode))
            {
                try
                {
                    var acceptResult = await referralService.AcceptAsync(
                        command.ReferralCode, userEntity.Id, cancellationToken);
                    if (!acceptResult.IsAccepted)
                    {
                        logger.LogInformation(
                            "Referral code {Code} not accepted for new user {UserId}: {Error}",
                            command.ReferralCode, userEntity.Id, acceptResult.Error);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to accept referral code {Code} for user {UserId}",
                        command.ReferralCode, userEntity.Id);
                }
            }

            return BusinessResult.Success();
        }
    }
}