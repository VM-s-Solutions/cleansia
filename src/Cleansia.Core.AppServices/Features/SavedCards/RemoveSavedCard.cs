using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.SavedCards;

/// <summary>
/// The customer removes their saved card: the row is deactivated, and the platform charges only the card
/// its own active row names, so nothing is charged to it again. The payment method is deliberately left on
/// the Stripe Customer: that Customer is the one per currency that Cleansia Plus also bills, and the card
/// may be the one a subscription renews on.
/// </summary>
public class RemoveSavedCard
{
    public record Command(string SavedCardId) : ICommand<Response>;

    public record Response(string SavedCardId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.SavedCardId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        ISavedCardRepository savedCardRepository,
        IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var card = await savedCardRepository.GetByIdAsync(command.SavedCardId, cancellationToken);
            if (card is null || !card.IsActive || card.UserId != userSessionProvider.GetUserId())
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.SavedCardId), BusinessErrorMessage.SavedCardNotFound));
            }

            savedCardRepository.Deactivate(card);

            return BusinessResult.Success(new Response(card.Id));
        }
    }
}
