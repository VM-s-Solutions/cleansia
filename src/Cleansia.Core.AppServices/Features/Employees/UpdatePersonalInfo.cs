#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

public class UpdatePersonalInfo
{
    public class Validator : BaseUserValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
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

            AddFirstNameRules(c => c.FirstName);
            AddLastNameRules(c => c.LastName);

            RuleFor(c => c.BirthDate)
                .Cascade(CascadeMode.Stop)
                .MustBeValidDate()
                .MustBeInPast()
                .MustBeReasonableAge();

            RuleFor(c => c.Phone)
                .ValidatePhoneNumber();

            AddEmailRules(c => c.Email);
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
        string FirstName,
        string LastName,
        DateOnly BirthDate,
        string Phone,
        string Email) : ICommand<Response>;

    public record Response(string EmployeeId);

    internal class Handler(
        IEmployeeRepository employeeRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);

            employee!.User!.Update(
                command.FirstName,
                command.LastName,
                command.Phone,
                command.BirthDate);

            return BusinessResult.Success(new Response(employee.Id));
        }
    }
}
