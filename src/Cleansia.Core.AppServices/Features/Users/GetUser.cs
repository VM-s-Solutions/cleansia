using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
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

    public class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider)
        : IQueryHandler<Query, UserItem>
    {
        public async Task<BusinessResult<UserItem>> Handle(Query query, CancellationToken cancellationToken)
        {
            // Inner ownership gate (ADR-0001 §D3): a non-admin caller may only resolve
            // their own user record. Mirrors GetPeriodPays — a non-owner gets the not-found business
            // error rather than the other user's PII. The policy is the outer gate; this holds on any
            // invocation path.
            var role = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value;
            if (role != UserProfile.Administrator.ToString() &&
                query.UserId != userSessionProvider.GetUserId())
            {
                return BusinessResult.Failure<UserItem>(new Error(
                    nameof(Query.UserId), BusinessErrorMessage.NotExistingUserWithId));
            }

            var user = await userRepository.GetByIdAsync(query.UserId, cancellationToken);

            return BusinessResult.Success(user!.MapToDetailDto())!;
        }
    }
}