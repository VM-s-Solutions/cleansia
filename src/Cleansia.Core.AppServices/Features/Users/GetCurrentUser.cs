using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Users;

public class GetCurrentUser
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

    public record Query : IQuery<MyProfileDto>;

    public class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider)
        : IQueryHandler<Query, MyProfileDto>
    {
        public async Task<BusinessResult<MyProfileDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(userSessionProvider.GetUserEmail()!, cancellationToken);
            return BusinessResult.Success(user!.MapToMyProfileDto()!);
        }
    }
}
