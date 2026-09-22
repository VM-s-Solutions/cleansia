using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Auth;

public class RegisterEmployee
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
        }

        // One identity per email across the holding (ADR-0061 D5.1); see Register.
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
        // The market the cleaner registers against and is held to at approval (ADR-0061 D3/D6);
        // null is the default market.
        string? CountryId = null,
        // The terms tick as the client asserted it; null or false is never refused, since the employee
        // agreement is ADR-0041's and does not gate registration.
        bool? TermsAccepted = null)
        : ICommand, IOperatorScopedRequest;

    public class Handler(
        IUserRepository userRepository,
        IEmployeeRepository employeeRepository,
        IPendingDispatch pending,
        IConsentService consentService)
        : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
        {
            // Same resolution the validator proved: a non-null row here is unconfirmed and in this market.
            var userEntity = await userRepository.GetByEmailIgnoringTenantAsync(command.Email, cancellationToken);
            // Email the RAW confirmation token; the entity persists only its hash.
            // New user -> raw from CreateWithPassword; existing unconfirmed user -> refresh to get a raw
            // token (the stored ConfirmationCode is a hash and cannot be emailed).
            string rawConfirmationToken;
            if (userEntity is null)
            {
                userEntity = User.CreateWithPassword(command.Email, command.Password, command.FirstName, command.LastName, UserProfile.Employee, command.Language);
                rawConfirmationToken = userEntity.RawConfirmationToken!;
                userRepository.Add(userEntity);
                employeeRepository.Add(Employee.CreateWithUser(userEntity));

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
                rawConfirmationToken = userEntity.UpdateConfirmationCode();
                userEntity.UpgradeToEmployee();
            }

            if (userEntity.Employee is null)
            {
                employeeRepository.Add(Employee.CreateWithUser(userEntity));
            }

            // No document: an employee accepts a different text than the customer documents (ADR-0041),
            // so the row stays unversioned until those land — the same rule GrantConsent applies.
            if (command.TermsAccepted == true)
            {
                await consentService.TryGrantAsync(userEntity.Id, ConsentType.TermsOfService, null, cancellationToken);
                await consentService.TryGrantAsync(userEntity.Id, ConsentType.PrivacyPolicy, null, cancellationToken);
            }

            var userName = $"{userEntity.FirstName} {userEntity.LastName}";

            EmailDispatch.EnqueueConfirmation(pending, userEntity, userName, rawConfirmationToken, command.Language);

            return BusinessResult.Success();
        }
    }
}