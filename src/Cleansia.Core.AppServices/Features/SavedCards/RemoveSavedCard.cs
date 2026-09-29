using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.SavedCards;

/// <summary>
/// The customer removes their saved card: the row is deactivated, so nothing is charged to it again, and
/// the card is detached from the Stripe Customer. A detach Stripe refuses or cannot reach does not keep
/// the card: the platform charges only the card its own row names, and that row is gone.
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
        IUserSessionProvider userSessionProvider,
        IStripeClient stripeClient,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
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

            if (card.StripePaymentMethodId is { } paymentMethodId)
            {
                try
                {
                    await stripeClient.DetachPaymentMethodAsync(paymentMethodId, cancellationToken);
                }
                catch (StripeException ex)
                {
                    logger.LogWarning(ex,
                        "Stripe could not detach payment method of saved card {SavedCardId}; the card is removed all the same",
                        card.Id);
                }
            }

            return BusinessResult.Success(new Response(card.Id));
        }
    }
}
