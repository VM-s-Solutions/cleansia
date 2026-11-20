using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.SavedCards.DTOs;
using Cleansia.Core.AppServices.Features.SavedCards.Mappers;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.SavedCards;

/// <summary>
/// The app confirms the card its sheet just saved from Payments, so the card is listed at once instead of
/// waiting on the setup_intent.succeeded webhook, which stays as the backstop. The SetupIntent is read from
/// Stripe: only the caller's own active row that its metadata names, on that row's Stripe Customer, is
/// completed, through the webhook's own <see cref="SavedCardCapture"/>. Whichever of the two comes second
/// finds the card captured and changes nothing. Everything else is the same not-found refusal.
/// </summary>
public class ConfirmSavedCardSetup
{
    public record Command(string SetupIntentId) : ICommand<SavedCardDto>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.SetupIntentId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(
        ISavedCardRepository savedCardRepository,
        IUserSessionProvider userSessionProvider,
        IStripeClient stripeClient,
        ICurrencyRepository currencyRepository,
        ILogger<Handler> logger) : ICommandHandler<Command, SavedCardDto>
    {
        public async Task<BusinessResult<SavedCardDto>> Handle(Command command, CancellationToken cancellationToken)
        {
            try
            {
                var intent = await stripeClient.GetSetupIntentCardAsync(command.SetupIntentId, cancellationToken);
                var card = string.IsNullOrEmpty(intent.SavedCardId)
                    ? null
                    : await savedCardRepository.GetByIdAsync(intent.SavedCardId, cancellationToken);
                if (card is null
                    || !card.IsActive
                    || card.UserId != userSessionProvider.GetUserId()
                    || card.StripeCustomerId != intent.CustomerId
                    || (!card.IsCaptured && intent.Card is null))
                {
                    return BusinessResult.Failure<SavedCardDto>(new Error(
                        nameof(command.SetupIntentId), BusinessErrorMessage.SavedCardNotFound));
                }

                if (!card.IsCaptured)
                {
                    await savedCardRepository.CaptureAsync(stripeClient, card, intent.Card!, cancellationToken);
                }

                var currency = await currencyRepository.GetByIdAsync(card.CurrencyId, cancellationToken);
                return BusinessResult.Success(card.MapToDto(currency!.Code));
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Stripe confirm of setup intent {SetupIntentId} failed", command.SetupIntentId);
                return BusinessResult.Failure<SavedCardDto>(new Error(
                    nameof(command.SetupIntentId), BusinessErrorMessage.PaymentGatewayUnavailable));
            }
        }
    }
}
