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
/// The customer removes their saved card: it is taken off the Stripe Customer, so no later sheet offers it,
/// and the row is deactivated, so the platform never charges it again. A card a Plus subscription renews on
/// stays on the Customer, which Plus also bills. Cash bookings already made keep their tender, and a paid
/// order's refunds go to its charge, so neither needs the card.
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

            if (!string.IsNullOrEmpty(card.StripePaymentMethodId))
            {
                try
                {
                    await stripeClient.DetachSavedCardAsync(card.StripeCustomerId, card.StripePaymentMethodId, cancellationToken);
                }
                catch (StripeException ex)
                {
                    logger.LogError(ex, "Stripe detach of saved card {SavedCardId} failed", card.Id);
                    return BusinessResult.Failure<Response>(new Error(
                        nameof(command.SavedCardId), BusinessErrorMessage.PaymentGatewayUnavailable));
                }
            }

            savedCardRepository.Deactivate(card);

            return BusinessResult.Success(new Response(card.Id));
        }
    }
}
