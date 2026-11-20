using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Features.SavedCards;

/// <summary>
/// The one way a pending saved card becomes the customer's card, for the webhook and for the app's confirm
/// alike: the card Stripe saved lands on the row, and the customer's earlier card in that currency is
/// retired and taken off the Stripe Customer, unless Plus renews on it. The caller has checked that the row
/// is still pending and active. An unreachable Stripe throws before any row changes, and a retry leaves a card
/// already taken off alone.
/// </summary>
public static class SavedCardCapture
{
    public static async Task CaptureAsync(
        this ISavedCardRepository savedCardRepository,
        IStripeClient stripeClient,
        SavedCard card,
        SavedCardDetails details,
        CancellationToken cancellationToken)
    {
        // The two events of one payment arrive together: the later one finds this very card already captured by
        // the earlier one's commit, and must not switch it off as a card it replaces.
        var replaced = (await savedCardRepository.GetCapturedForUserInCurrencyAsync(card.UserId, card.CurrencyId, cancellationToken))
            .Where(c => c.Id != card.Id)
            .ToList();

        // A sheet offers the Customer's attached cards, so the earlier row can name the very card being saved now.
        foreach (var earlier in replaced.Where(c =>
                     !string.IsNullOrEmpty(c.StripePaymentMethodId) && c.StripePaymentMethodId != details.PaymentMethodId))
        {
            await stripeClient.DetachSavedCardAsync(earlier.StripeCustomerId, earlier.StripePaymentMethodId!, cancellationToken);
        }

        foreach (var earlier in replaced)
        {
            savedCardRepository.Deactivate(earlier);
        }

        card.Capture(details.PaymentMethodId, details.Brand, details.Last4, details.ExpMonth, details.ExpYear);
    }
}
