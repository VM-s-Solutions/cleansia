#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

public class UpdateIdentificationInfo
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserSessionProvider _userSessionProvider;
        private readonly ITaxIdValidator _taxIdValidator;

        public Validator(
            ICountryRepository countryRepository,
            IEmployeeRepository employeeRepository,
            IUserSessionProvider userSessionProvider,
            ITaxIdValidator taxIdValidator)
        {
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _userSessionProvider = userSessionProvider ?? throw new ArgumentNullException(nameof(userSessionProvider));
            _taxIdValidator = taxIdValidator ?? throw new ArgumentNullException(nameof(taxIdValidator));

            RuleFor(c => c)
                .MustAsync(CallerIsAnEmployee)
                .WithMessage(BusinessErrorMessage.NotAllowedToUpdateEmployee);

            RuleFor(c => c.NationalityId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(countryRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotExistingCountryWithId);

            RuleFor(c => c.PassportId)
                .ValidatePassportId();

            // CountryId scopes the IČO/VAT format check — different countries
            // have different patterns. Required because the validator below
            // can't run without it.
            RuleFor(c => c.BusinessCountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(countryRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotExistingCountryWithId);

            // Mirror UpdateEmployee's business-field rules so the partner can
            // partial-update identification with the same checks the big
            // self-update applies. RegistrationNumber is required at the
            // domain level (it's part of IsProfileComplete), but the format
            // validator only runs when the value is non-blank so an empty
            // value reports as "required" first instead of "invalid format".
            RuleFor(c => c.RegistrationNumber)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded);

            RuleFor(c => c.RegistrationNumber)
                .MustAsync(async (command, value, ct) =>
                {
                    var result = await _taxIdValidator.ValidateRegistrationNumberAsync(
                        command.BusinessCountryId, command.EntityType, value, ct);
                    return result.IsValid;
                })
                .WithMessage(BusinessErrorMessage.RegistrationNumberInvalidFormat)
                .When(c => !string.IsNullOrWhiteSpace(c.RegistrationNumber));

            RuleFor(c => c.VatNumber)
                .MaximumLength(50)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded)
                .When(c => !string.IsNullOrWhiteSpace(c.VatNumber));

            RuleFor(c => c.VatNumber)
                .MustAsync(async (command, value, ct) =>
                {
                    var result = await _taxIdValidator.ValidateVatNumberAsync(
                        command.BusinessCountryId, value, ct);
                    return result.IsValid;
                })
                .WithMessage(BusinessErrorMessage.VatNumberInvalidFormat)
                .When(c => !string.IsNullOrWhiteSpace(c.VatNumber));

            // Legal entity name only required when EntityType=LegalEntity.
            // For natural persons the field is ignored (handler clears it).
            RuleFor(c => c.LegalEntityName)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(200)
                .WithMessage(BusinessErrorMessage.MaxLengthExceeded)
                .When(c => c.EntityType == EmployeeEntityType.LegalEntity);
        }

        // Not an ownership comparison — the subject is server-resolved, so there is nothing for a client
        // to get wrong. What survives is the precondition the handler dereferences.
        private async Task<bool> CallerIsAnEmployee(Command command, CancellationToken cancellationToken)
        {
            var employee = await _employeeRepository.GetByUserEmailAsync(
                _userSessionProvider.GetUserEmail() ?? string.Empty, cancellationToken);
            return employee is not null;
        }
    }

    public record Command(
        // [OWN-DATA] (S1): inert. The record written is always the JWT caller's; this stays on the wire
        // only so the shipped clients keep serializing unchanged. Nullable is load-bearing — a
        // non-nullable reference member makes MVC reject an ABSENT id before MediatR is reached.
        string? EmployeeId,
        string NationalityId,
        string PassportId,
        EmployeeEntityType EntityType,
        string BusinessCountryId,
        string RegistrationNumber,
        string? VatNumber,
        string? LegalEntityName) : ICommand<Response>;

    public record Response(string EmployeeId);

    internal class Handler(
        IEmployeeRepository employeeRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByUserEmailAsync(
                userSessionProvider.GetUserEmail() ?? string.Empty, cancellationToken);

            if (employee is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(BusinessErrorMessage.EmployeeNotFound), BusinessErrorMessage.EmployeeNotFound));
            }

            employee.UpdateIdentification(
                command.NationalityId,
                command.PassportId);

            // Business identity is a separate domain method — keeps the
            // "who you are" (nationality + passport) and "how your business
            // is registered" (entity type + IČO + VAT + legal name)
            // concerns aligned with how the Employee aggregate exposes them.
            employee.UpdateBusinessIdentity(
                command.EntityType,
                command.RegistrationNumber,
                command.VatNumber,
                command.LegalEntityName);

            return BusinessResult.Success(new Response(employee.Id));
        }
    }
}
