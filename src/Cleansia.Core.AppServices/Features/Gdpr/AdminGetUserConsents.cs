using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
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

    internal class Handler(IUserConsentRepository userConsentRepository, ILegalDocumentResolver legalDocumentResolver)
        : IQueryHandler<Query, List<UserConsentDto>>
    {
        public async Task<BusinessResult<List<UserConsentDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var consents = await userConsentRepository.GetByUserIdNoTrackingAsync(request.UserId, cancellationToken);

            return BusinessResult.Success(await consents.MapToDtosAsync(legalDocumentResolver, cancellationToken));
        }
    }
}
