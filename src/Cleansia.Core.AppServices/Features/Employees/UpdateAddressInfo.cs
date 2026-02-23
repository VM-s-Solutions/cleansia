#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

public class UpdateAddressInfo
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            ICountryRepository countryRepository,
            IEmployeeRepository employeeRepository,
            IUserSessionProvider userSessionProvider)
        {
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _userSessionProvider = userSessionProvider ?? throw new ArgumentNullException(nameof(userSessionProvider));

            RuleFor(c => c)
                .MustAsync(AllowedToUpdateEmployee)
                .WithMessage(BusinessErrorMessage.NotAllowedToUpdateEmployee);

            RuleFor(c => c.EmployeeId)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotFound);

            RuleFor(c => c.Street)
                .ValidateStreetAddress();

            RuleFor(c => c.City)
                .ValidateCity();

            RuleFor(c => c.ZipCode)
                .ValidateZipCode();

            RuleFor(c => c.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(countryRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.NotExistingCountryWithId);
        }

        private async Task<bool> AllowedToUpdateEmployee(Command command, CancellationToken cancellationToken)
        {
            var currentUserEmail = _userSessionProvider.GetUserEmail();
            var employee = await _employeeRepository.GetByUserEmailAsync(currentUserEmail ?? string.Empty, cancellationToken);
            return employee?.Id == command.EmployeeId;
        }
    }

    public record Command(
        string EmployeeId,
        string Street,
        string City,
        string ZipCode,
        string CountryId) : ICommand<Response>;

    public record Response(string EmployeeId);

    internal class Handler(
        IEmployeeRepository employeeRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);

            var address = employee!.Address is not null
                ? employee.Address.Update(command.Street, command.City, command.ZipCode, command.CountryId)
                : Address.Create(command.Street, command.City, command.ZipCode, command.CountryId);

            employee.UpdateAddress(address);

            return BusinessResult.Success(new Response(employee.Id));
        }
    }
}
