using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.SavedCards;

/// <summary>
/// The web card capture: the consent is recorded on a new saved-card row, and the customer is sent to a
/// Stripe Checkout Session in setup mode on their Stripe Customer for the market's currency. Nothing is
/// charged. The card lands on the row when the checkout.session.completed webhook arrives.
/// </summary>
public class CreateSavedCardCheckoutSession
{
    /// <param name="ConsentAccepted">The customer ticked the consent that fees and unpaid cash may be charged to the card.</param>
    /// <param name="CountryId">The market the card guarantees bookings in; null is the platform default market.</param>
    public record Command(bool ConsentAccepted, string? CountryId = null) : ICommand<Response>;

    public record Response(string SavedCardId, string CheckoutUrl);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICountryRepository countryRepository)
        {
            RuleFor(x => x.ConsentAccepted)
                .Cascade(CascadeMode.Stop)
                .Equal(true).WithMessage(BusinessErrorMessage.SavedCardConsentNotAccepted);

            RuleFor(x => x.CountryId)
                .Cascade(CascadeMode.Stop)
                .MustAsync((countryId, ct) => countryRepository.IsServicedAsync(countryId!, ct))
                .WithMessage(BusinessErrorMessage.CountryNotServiced)
                .When(x => !string.IsNullOrEmpty(x.CountryId));
        }
    }

    public class Handler(
        IUserRepository userRepository,
        ISavedCardRepository savedCardRepository,
        ICurrencyResolutionService currencyResolutionService,
        IStripeCustomerResolver stripeCustomerResolver,
        IUserSessionProvider userSessionProvider,
        IRequestMetadataProvider requestMetadataProvider,
        IStripeClient stripeClient,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
            {
                return BusinessResult.Failure<Response>(new Error(nameof(userId), BusinessErrorMessage.UserNotFound));
            }

            var currency = await currencyResolutionService.ResolveCurrencyForCountryAsync(command.CountryId, cancellationToken);

            string checkoutUrl;
            SavedCard card;
            try
            {
                var stripeCustomerId = await stripeCustomerResolver.ResolveForCurrencyAsync(user, currency, cancellationToken);
                card = SavedCard.Start(
                    user.Id, currency.Id, stripeCustomerId, requestMetadataProvider.IpAddress, requestMetadataProvider.DeviceLabel);
                checkoutUrl = await stripeClient.CreateCardSetupCheckoutSessionAsync(stripeCustomerId, card.Id, cancellationToken);
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Stripe card-setup checkout failed for user {UserId}", user.Id);
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CountryId), BusinessErrorMessage.PaymentGatewayUnavailable));
            }

            savedCardRepository.Add(card);
            return BusinessResult.Success(new Response(card.Id, checkoutUrl));
        }
    }
}
