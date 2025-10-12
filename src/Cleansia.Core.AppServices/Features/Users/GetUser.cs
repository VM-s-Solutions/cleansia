using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Users;

public class GetUser
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(user => user.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(userRepository.ExistsAsync)
                .WithErrorCode(nameof(Query.UserId))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithId);
        }
    }

    public record Query(
        string UserId)
        : IQuery<UserItem>;

    internal class Handler(
        IUserRepository userRepository)
        : IQueryHandler<Query, UserItem>
    {
        public async Task<BusinessResult<UserItem>> Handle(Query query, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByIdAsync(query.UserId, cancellationToken);

            return BusinessResult.Success(user!.MapToDetailDto())!;
        }
    }
}