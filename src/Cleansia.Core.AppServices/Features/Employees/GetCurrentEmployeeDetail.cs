using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

public class GetCurrentEmployeeDetail
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

    public record Query : IQuery<EmployeeItem>;

    public class Handler(
        IEmployeeRepository employeeRepository,
        IUserSessionProvider userSessionProvider)
        : IQueryHandler<Query, EmployeeItem>
    {
        public async Task<BusinessResult<EmployeeItem>> Handle(Query query, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByUserEmailAsync(userSessionProvider.GetUserEmail()!, cancellationToken);
            return BusinessResult.Success(employee!.MapToEmployeeItem()!);
        }
    }
}