#nullable enable
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Attributes;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

public class UpdatePersonalInfo
{
    public class Validator : BaseUserValidator<Command>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserSessionProvider _userSessionProvider;
        private readonly IUserRepository _userRepository;

        public Validator(
            IEmployeeRepository employeeRepository,
            IUserSessionProvider userSessionProvider,
            IUserRepository userRepository)
        {
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _userSessionProvider = userSessionProvider ?? throw new ArgumentNullException(nameof(userSessionProvider));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

            RuleFor(c => c)
                .MustAsync(CallerIsAnEmployee)
                .WithMessage(BusinessErrorMessage.NotAllowedToUpdateEmployee);

            AddFirstNameRules(c => c.FirstName);
            AddLastNameRules(c => c.LastName);

            RuleFor(c => c.BirthDate)
                .Cascade(CascadeMode.Stop)
                .MustBeValidDate()
                .MustBeInPast()
                .MustBeReasonableAge();

            // Not in the shared ValidatePhoneNumber: the web profile save accepts numbers without a
            // country code, which this rule refuses.
            RuleFor(c => c.Phone)
                .Cascade(CascadeMode.Stop)
                .ValidatePhoneNumber()
                .Must(phone => new PhoneNumberAttribute().IsValid(phone))
                .WithMessage(BusinessErrorMessage.InvalidPhoneNumber)
                .MustAsync(PhoneIsAvailableAsync)
                .WithMessage(BusinessErrorMessage.ExistingPhoneNumber)
                .WithErrorCode(nameof(Command.Phone));
        }

        private async Task<bool> PhoneIsAvailableAsync(string? phone, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(phone!, cancellationToken);
            return user is null || user.Id == _userSessionProvider.GetUserId();
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
        string FirstName,
        string LastName,
        DateOnly BirthDate,
        // Nullable for the same reason: an absent phone must reach the validator's common.required.
        string? Phone) : ICommand<Response>;

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

            employee.User!.Update(
                command.FirstName,
                command.LastName,
                command.Phone,
                command.BirthDate);

            return BusinessResult.Success(new Response(employee.Id));
        }
    }
}
