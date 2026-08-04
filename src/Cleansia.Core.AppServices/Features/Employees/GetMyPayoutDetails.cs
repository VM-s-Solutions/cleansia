using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

/// <summary>
/// The cleaner's own payout destination, unmasked — it is their account and they must be able to check
/// it before a payroll run keys a transfer from it (ADR-0034 D8.2, S4 self-data). The subject is
/// resolved from the session, never from the request.
/// </summary>
public class GetMyPayoutDetails
{
    public class Validator : AbstractValidator<Query>;

    public record Query : IQuery<MyPayoutDetails>;

    public class Handler(
        IEmployeeRepository employeeRepository,
        IEmployeePayoutDetailsRepository payoutDetailsRepository,
        IUserSessionProvider userSessionProvider)
        : IQueryHandler<Query, MyPayoutDetails>
    {
        public async Task<BusinessResult<MyPayoutDetails>> Handle(Query query, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByUserEmailAsync(
                userSessionProvider.GetUserEmail() ?? string.Empty, cancellationToken);

            if (employee is null)
            {
                return BusinessResult.Failure<MyPayoutDetails>(
                    new Error(nameof(BusinessErrorMessage.EmployeeNotFound), BusinessErrorMessage.EmployeeNotFound));
            }

            var details = await payoutDetailsRepository.GetByEmployeeIdAsync(employee.Id, cancellationToken);

            return details is null
                ? BusinessResult.Failure<MyPayoutDetails>(
                    new Error(nameof(BusinessErrorMessage.PayoutDetailsNotFound), BusinessErrorMessage.PayoutDetailsNotFound))
                : BusinessResult.Success(details.MapToMyDto());
        }
    }
}
