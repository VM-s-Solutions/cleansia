using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

[AuditAction("employee.reject", ResourceType = "User")]
public class RejectEmployee
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator(IEmployeeRepository employeeRepository)
        {
            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                    .WithMessage(BusinessErrorMessage.EmployeeNotFound);

            RuleFor(x => x.EmployeeId)
                .MustAsync(async (employeeId, cancellationToken) =>
                {
                    var employee = await employeeRepository.GetByIdAsync(employeeId, cancellationToken);
                    return employee?.ContractStatus != ContractStatus.Rejected;
                })
                .WithMessage(BusinessErrorMessage.EmployeeAlreadyRejected)
                .When(x => !string.IsNullOrEmpty(x.EmployeeId));

            When(x => !string.IsNullOrEmpty(x.Reason), () =>
            {
                RuleFor(x => x.Reason)
                    .MaximumLength(500).WithMessage(BusinessErrorMessage.MaxLength);
            });
        }
    }

    public record Request(string? Reason);

    public record Command(string EmployeeId, string? Reason = null) : ICommand<Response>;

    public record Response(string EmployeeId, DateTimeOffset RejectedAt);

    // Keyed on the USER id (the audited subject the employee drill-in filters on), never the Employee
    // id. The admin's free-text reason is excluded — it could carry subject PII.
    public record ContractSnapshot(string UserId, string EmployeeId, ContractStatus Status);

    public class Handler(
        IEmployeeRepository employeeRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IAuditContext auditContext)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var adminEmail = userSessionProvider.GetUserEmail();
            var adminUser = await userRepository.GetByEmailAsync(adminEmail!, cancellationToken);

            if (adminUser is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    "Authentication",
                    BusinessErrorMessage.UserNotFound));
            }

            var employee = await employeeRepository.GetByIdAsync(command.EmployeeId, cancellationToken);

            if (employee is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(Command.EmployeeId),
                    BusinessErrorMessage.EmployeeNotFound));
            }

            var statusBefore = employee.ContractStatus;

            employee.Reject(adminUser.Id, command.Reason);

            auditContext.RecordChange(
                "User",
                employee.UserId,
                new ContractSnapshot(employee.UserId, employee.Id, statusBefore),
                new ContractSnapshot(employee.UserId, employee.Id, employee.ContractStatus));

            return BusinessResult.Success(new Response(employee.Id, employee.RejectedAt!.Value));
        }
    }
}
