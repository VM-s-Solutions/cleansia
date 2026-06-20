using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class AdminGetUserConsents
{
    public record Query(string UserId) : IQuery<List<UserConsentDto>>;

    internal class Validator : AbstractValidator<Query>
    {
        public Validator(IUserRepository userRepository)
        {
            RuleFor(q => q.UserId)
                .NotEmpty()
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithId);
        }
    }

    internal class Handler(IUserConsentRepository userConsentRepository)
        : IQueryHandler<Query, List<UserConsentDto>>
    {
        public async Task<BusinessResult<List<UserConsentDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var consents = await userConsentRepository.GetByUserIdNoTrackingAsync(request.UserId, cancellationToken);
            var dtos = consents.Select(c => new UserConsentDto(
                c.Id, c.ConsentType, c.IsGranted,
                c.GrantedAt, c.WithdrawnAt, c.CreatedOn)).ToList();

            return BusinessResult.Success(dtos);
        }
    }
}
