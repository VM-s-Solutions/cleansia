using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Users;

public class GetUserByEmail
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(user => user.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(userRepository.ExistsWithEmailAsync)
                .WithErrorCode(nameof(Query.Email))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithEmail);
        }
    }

    public record Query(
        string Email)
        : IQuery<UserListItem>;

    internal class Handler(
        IUserRepository userRepository)
        : IQueryHandler<Query, UserListItem>
    {
        public async Task<BusinessResult<UserListItem>> Handle(Query query, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailAsync(query.Email, cancellationToken);
            return BusinessResult.Success(user!.MapToDto()!);
        }
    }
}