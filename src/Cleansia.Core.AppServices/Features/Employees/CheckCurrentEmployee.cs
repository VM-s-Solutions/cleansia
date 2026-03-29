using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Employees;

public class CheckCurrentEmployee
{
    public class Validator : AbstractValidator<Query>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            IUserRepository userRepository,
            IEmployeeRepository employeeRepository,
            IUserSessionProvider userSessionProvider)
        {
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _userSessionProvider = userSessionProvider ?? throw new ArgumentNullException(nameof(userSessionProvider));

            RuleFor(query => query)
                .SetValidator(new UserEmailValidator<Query>(userRepository, userSessionProvider));

            RuleFor(query => query)
                .MustAsync(ExistsWithUserEmailAsync)
                .WithMessage(BusinessErrorMessage.NotExistingUserWithEmail);
        }

        private Task<bool> ExistsWithUserEmailAsync(Query query, CancellationToken cancellationToken)
        {
            var userEmail = _userSessionProvider.GetUserEmail();
            return userEmail is null
                ? Task.FromResult(false)
                : _employeeRepository.ExistsWithUserEmailAsync(userEmail, cancellationToken);
        }
    }

    public record Query : IQuery<RegistrationCompletionStatus>;

    public class Handler(
        IEmployeeRepository employeeRepository,
        IUserSessionProvider userSessionProvider)
        : IQueryHandler<Query, RegistrationCompletionStatus>
    {
        public async Task<BusinessResult<RegistrationCompletionStatus>> Handle(Query query, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository.GetByUserEmailAsync(userSessionProvider.GetUserEmail()!, cancellationToken);
            return BusinessResult.Success(employee!.ToRegistrationCompletionStatus());
        }
    }
}
