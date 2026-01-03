using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Employees;

public class ApproveEmployee
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
                    return employee?.ContractStatus != ContractStatus.Approved;
                })
                .WithMessage(BusinessErrorMessage.EmployeeAlreadyApproved)
                .When(x => !string.IsNullOrEmpty(x.EmployeeId));

            RuleFor(x => x.EmployeeId)
                .MustAsync(async (employeeId, cancellationToken) =>
                {
                    var employee = await employeeRepository
                        .GetQueryable()
                        .Include(e => e.User)
                        .Include(e => e.Address)
                        .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

                    return employee?.IsProfileComplete() ?? false;
                })
                .WithMessage(BusinessErrorMessage.EmployeeProfileIncomplete)
                .When(x => !string.IsNullOrEmpty(x.EmployeeId));

            When(x => !string.IsNullOrEmpty(x.Notes), () =>
            {
                RuleFor(x => x.Notes)
                    .MaximumLength(1000).WithMessage(BusinessErrorMessage.MaxLength);
            });
        }
    }

    public record Request(string? Notes);

    public record Command(string EmployeeId, string? Notes = null) : ICommand<Response>;

    public record Response(string EmployeeId, DateTimeOffset ApprovedAt);

    public class Handler(
        IEmployeeRepository employeeRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider)
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

            var employee = await employeeRepository
                .GetQueryable()
                .Include(e => e.User)
                .Include(e => e.Address)
                .FirstOrDefaultAsync(e => e.Id == command.EmployeeId, cancellationToken);

            if (employee is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(Command.EmployeeId),
                    BusinessErrorMessage.EmployeeNotFound));
            }

            if (!employee.IsProfileComplete())
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(Command.EmployeeId),
                    BusinessErrorMessage.EmployeeProfileIncomplete));
            }

            employee.Approve(adminUser.Id, command.Notes);

            return BusinessResult.Success(new Response(employee.Id, employee.ApprovedAt!.Value));
        }
    }
}
