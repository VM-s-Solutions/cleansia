using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class GetUserConsents
{
    public record Query : IQuery<List<UserConsentDto>>;

    internal class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IUserConsentRepository userConsentRepository)
        : IQueryHandler<Query, List<UserConsentDto>>
    {
        public async Task<BusinessResult<List<UserConsentDto>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var email = userSessionProvider.GetUserEmail();
            var user = await userRepository.GetByEmailAsync(email!, cancellationToken);

            if (user is null)
                return BusinessResult.Failure<List<UserConsentDto>>(new Error(
                    BusinessErrorMessage.NotExistingUserWithEmail, "User not found"));

            var consents = await userConsentRepository.GetByUserIdAsync(user.Id, cancellationToken);
            var dtos = consents.Select(c => new UserConsentDto(
                c.Id, c.ConsentType, c.IsGranted,
                c.GrantedAt, c.WithdrawnAt, c.CreatedOn)).ToList();

            return BusinessResult.Success(dtos);
        }
    }
}
