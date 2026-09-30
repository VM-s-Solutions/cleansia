using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class GetUserConsents
{
    public record Query : IQuery<List<UserConsentDto>>;

    internal class Handler(
        IUserSessionProvider userSessionProvider,
        IUserConsentRepository userConsentRepository,
        ILegalDocumentResolver legalDocumentResolver)
        : IQueryHandler<Query, List<UserConsentDto>>
    {
        public async Task<BusinessResult<List<UserConsentDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            // userId is non-null past the controller's [Permission] gate.
            var userId = userSessionProvider.GetUserId()!;
            var consents = await userConsentRepository.GetByUserIdNoTrackingAsync(userId, cancellationToken);

            return BusinessResult.Success(await consents.MapToDtosAsync(legalDocumentResolver, cancellationToken));
        }
    }
}
