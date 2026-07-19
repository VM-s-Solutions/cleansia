using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Employees;

[AuditAction("employee.approve", ResourceType = "User")]
public class ApproveEmployee
{
    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            IEmployeeRepository employeeRepository,
            ICountryRepository countryRepository)
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
                        .Include(e => e.Documents)
                        .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

                    return employee?.IsProfileComplete() ?? false;
                })
                .WithMessage(BusinessErrorMessage.EmployeeProfileIncomplete)
                .When(x => !string.IsNullOrEmpty(x.EmployeeId));

            // WorkCountryId is the jurisdiction the cleaner is approved
            // to take jobs in. Drives currency / language / VAT defaults
            // via CountryConfiguration. Required at approval time so the
            // resolver has a deterministic answer; must be a country we
            // actually service or the cleaner couldn't legally work it.
            RuleFor(x => x.WorkCountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.CountryRequired)
                .MustAsync(countryRepository.ExistsAsync)
                    .WithMessage(BusinessErrorMessage.CountryNotFound)
                .MustAsync(countryRepository.IsServicedAsync)
                    .WithMessage(BusinessErrorMessage.CountryNotServiced);

            When(x => !string.IsNullOrEmpty(x.Notes), () =>
            {
                RuleFor(x => x.Notes)
                    .MaximumLength(1000).WithMessage(BusinessErrorMessage.MaxLength);
            });
        }
    }

    public record Request(string WorkCountryId, string? Notes);

    public record Command(string EmployeeId, string WorkCountryId, string? Notes = null) : ICommand<Response>;

    public record Response(string EmployeeId, DateTimeOffset ApprovedAt);

    // Keyed on the USER id (the audited subject the employee drill-in filters on), never the Employee
    // id. The admin's free-text notes are excluded — they could carry subject PII.
    public record ContractSnapshot(string UserId, string EmployeeId, ContractStatus Status, string? WorkCountryId);

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

            var statusBefore = employee.ContractStatus;
            var workCountryBefore = employee.WorkCountryId;

            employee.AssignWorkCountry(command.WorkCountryId);
            employee.Approve(adminUser.Id, command.Notes);

            auditContext.RecordChange(
                "User",
                employee.UserId,
                new ContractSnapshot(employee.UserId, employee.Id, statusBefore, workCountryBefore),
                new ContractSnapshot(employee.UserId, employee.Id, employee.ContractStatus, employee.WorkCountryId));

            return BusinessResult.Success(new Response(employee.Id, employee.ApprovedAt!.Value));
        }
    }
}
