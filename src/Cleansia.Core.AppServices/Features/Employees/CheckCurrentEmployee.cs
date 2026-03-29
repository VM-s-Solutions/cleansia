using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

public class CheckCurrentEmployee
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider)
        {
            RuleFor(query => query)
                .SetValidator(new UserEmailValidator<Query>(userRepository, userSessionProvider));
        }
    }

    public record Query : IQuery<RegistrationCompletionStatus>;

    public class Handler(
        IEmployeeRepository employeeRepository,
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IUnitOfWork unitOfWork)
        : IQueryHandler<Query, RegistrationCompletionStatus>
    {
        public async Task<BusinessResult<RegistrationCompletionStatus>> Handle(Query query, CancellationToken cancellationToken)
        {
            var userEmail = userSessionProvider.GetUserEmail()!;
            var employee = await employeeRepository.GetByUserEmailAsync(userEmail, cancellationToken);

            if (employee is null)
            {
                // Auto-create Employee record for Customer users accessing Partner app
                var user = await userRepository.GetByEmailAsync(userEmail, cancellationToken);
                if (user is null)
                {
                    return BusinessResult.Failure<RegistrationCompletionStatus>(
                        new Error("Employee", BusinessErrorMessage.NotExistingUserWithEmail));
                }

                user.UpgradeToEmployee();
                employee = Employee.CreateWithUser(user);
                employeeRepository.Add(employee);
                await unitOfWork.CommitAsync(cancellationToken);
            }

            return BusinessResult.Success(employee.ToRegistrationCompletionStatus());
        }
    }
}
