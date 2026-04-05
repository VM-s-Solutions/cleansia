#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

public class AdminUpdateEmployee
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            ICountryRepository countryRepository,
            IEmployeeRepository employeeRepository)
        {
            RuleFor(c => c.EmployeeId)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotFound);

            RuleFor(c => c.FirstName)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(50).WithMessage(BusinessErrorMessage.MaxLengthExceeded);

            RuleFor(c => c.LastName)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(50).WithMessage(BusinessErrorMessage.MaxLengthExceeded);

            RuleFor(c => c.BirthDate)
                .MustBeValidDate()
                .MustBeInPast()
                .MustBeReasonableAge()
                .When(c => c.BirthDate != default);

            RuleFor(c => c.Phone)
                .ValidatePhoneNumber()
                .When(c => !string.IsNullOrWhiteSpace(c.Phone));

            RuleFor(c => c.Street).ValidateStreetAddress().When(c => !string.IsNullOrWhiteSpace(c.Street));
            RuleFor(c => c.City).ValidateCity().When(c => !string.IsNullOrWhiteSpace(c.City));
            RuleFor(c => c.ZipCode).ValidateZipCode().When(c => !string.IsNullOrWhiteSpace(c.ZipCode));

            RuleFor(c => c.NationalityId)
                .MustAsync(countryRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotExistingCountryWithId)
                .When(c => !string.IsNullOrWhiteSpace(c.NationalityId));

            RuleFor(c => c.CountryId)
                .MustAsync(countryRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotExistingCountryWithId)
                .When(c => !string.IsNullOrWhiteSpace(c.CountryId));

            RuleFor(c => c.RegistrationNumber)
                .MaximumLength(50).WithMessage(BusinessErrorMessage.MaxLengthExceeded)
                .When(c => !string.IsNullOrWhiteSpace(c.RegistrationNumber));

            RuleFor(c => c.VatNumber)
                .MaximumLength(50).WithMessage(BusinessErrorMessage.MaxLengthExceeded)
                .When(c => !string.IsNullOrWhiteSpace(c.VatNumber));

            RuleFor(c => c.LegalEntityName)
                .MaximumLength(200).WithMessage(BusinessErrorMessage.MaxLengthExceeded)
                .When(c => !string.IsNullOrWhiteSpace(c.LegalEntityName));

            RuleFor(c => c.Iban).ValidateIban().When(c => !string.IsNullOrWhiteSpace(c.Iban));
            RuleFor(c => c.PassportId).ValidatePassportId().When(c => !string.IsNullOrWhiteSpace(c.PassportId));
        }
    }

    public record Command(
        string EmployeeId,
        string? FirstName,
        string? LastName,
        DateOnly BirthDate,
        string? Phone,
        string? Street,
        string? City,
        string? ZipCode,
        string? CountryId,
        string? State,
        string? NationalityId,
        string? PassportId,
        EmployeeEntityType? EntityType,
        string? RegistrationNumber,
        string? VatNumber,
        string? LegalEntityName,
        string? Iban,
        string? EmergencyName,
        string? EmergencyPhone) : ICommand<Response>;

    public record Response(string EmployeeId);

    internal class Handler(IEmployeeRepository employeeRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);
            if (employee is null)
                return BusinessResult.Failure<Response>(new Error(nameof(command.EmployeeId), BusinessErrorMessage.NotFound));

            // Update user info
            if (!string.IsNullOrWhiteSpace(command.FirstName) || !string.IsNullOrWhiteSpace(command.LastName))
            {
                employee.User!.Update(
                    command.FirstName ?? employee.User.FirstName,
                    command.LastName ?? employee.User.LastName,
                    command.Phone ?? employee.User.PhoneNumber,
                    command.BirthDate != default ? command.BirthDate : employee.User.BirthDate);
            }

            Address address;
            if (!string.IsNullOrWhiteSpace(command.Street) || !string.IsNullOrWhiteSpace(command.City))
            {
                address = employee.Address is not null
                    ? employee.Address.Update(
                        command.Street ?? employee.Address.Street,
                        command.City ?? employee.Address.City,
                        command.ZipCode ?? employee.Address.ZipCode,
                        command.CountryId ?? employee.Address.CountryId,
                        command.State ?? employee.Address.State)
                    : Address.Create(
                        command.Street ?? "",
                        command.City ?? "",
                        command.ZipCode ?? "",
                        command.CountryId ?? "",
                        command.State);
            }
            else
            {
                address = employee.Address!;
            }

            // Admin edit is partial — preserve any fields the admin didn't explicitly supply.
            employee.UpdateEmployeeDetails(
                command.EntityType ?? employee.EntityType,
                command.RegistrationNumber ?? employee.RegistrationNumber,
                command.VatNumber ?? employee.VatNumber,
                command.LegalEntityName ?? employee.LegalEntityName,
                command.NationalityId ?? employee.NationalityId ?? "",
                command.PassportId ?? employee.PassportId ?? "",
                command.Iban ?? employee.IBAN ?? "",
                address,
                employee.Availability.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToList()),
                command.EmergencyName ?? employee.EmergencyContactName,
                command.EmergencyPhone ?? employee.EmergencyContactPhone);

            return BusinessResult.Success(new Response(employee.Id));
        }
    }
}
