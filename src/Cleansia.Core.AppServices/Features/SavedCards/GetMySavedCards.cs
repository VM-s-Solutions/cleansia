using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.SavedCards.DTOs;
using Cleansia.Core.AppServices.Features.SavedCards.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Features.SavedCards;

/// <summary>The caller's saved cards, one per currency at most; a capture Stripe has not yet confirmed is not one.</summary>
public class GetMySavedCards
{
    public record Query : IQuery<IReadOnlyList<SavedCardDto>>;

    public class Handler(
        ISavedCardRepository savedCardRepository,
        IUserSessionProvider userSessionProvider) : IQueryHandler<Query, IReadOnlyList<SavedCardDto>>
    {
        public async Task<BusinessResult<IReadOnlyList<SavedCardDto>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var cards = await savedCardRepository.GetCapturedForUserAsync(userSessionProvider.GetUserId()!, cancellationToken);
            return BusinessResult.Success<IReadOnlyList<SavedCardDto>>(cards.Select(c => c.MapToDto()).ToList());
        }
    }
}
