import CleansiaCore
import Foundation

extension BookingViewModel {
    /// How long a booking waits for a just-saved card: 8 reads, `pauseBetweenCardReads` apart.
    static let cardCaptureReads = 8

    /// Cash chosen, and the customer is known to hold no usable card in the booking's currency: the
    /// review step shows the card-guarantee consent, and the slide waits for its tick. An unanswered
    /// card read asks nothing; the submit reads the cards again before it books.
    var needsCardGuarantee: Bool {
        guard state.paymentMethod == .cash, let savedCards, let currencyCode = displayCurrencyCode else {
            return false
        }
        return SavedCard.usable(in: savedCards, currencyCode: currencyCode) == nil
    }

    func setCardGuaranteeAccepted(_ accepted: Bool) {
        update { current in
            var next = current
            next.cardGuaranteeAccepted = accepted
            return next
        }
    }

    var offersCardSaving: Bool {
        state.paymentMethod == .card && isCardPaymentAvailable && tokenStore.current() != nil
    }

    func setSaveCard(_ save: Bool) {
        update { current in
            var next = current
            next.saveCard = save
            return next
        }
    }

    @discardableResult
    func refreshSavedCards() async -> ApiResult<[SavedCard]> {
        let result = await savedCardClient.myCards()
        if case let .success(cards) = result {
            savedCards = cards
        }
        return result
    }

    /// Cash goes out only on a fresh quote that allows it and a usable saved card in the booking's
    /// currency; otherwise the outcome that stops it. Nil lets the order go out.
    func cashStep(
        _ paymentMethod: PaymentMethod,
        quote: BookingQuote,
        current: BookingState
    ) async -> BookingSubmitOutcome? {
        guard paymentMethod == .cash else { return nil }
        if takeCashAwayIfRefused(paymentMethod, by: quote) {
            return .paymentMethodCleared
        }
        return await cardGuaranteeStep(for: current, quote: quote)
    }

    /// Read from the server's cards at the moment of booking, not the list the review step showed. A card
    /// PaymentSheet already saved is waited for, never captured a second time: a slide after "still saving"
    /// or after a time that stopped holding books on the card already on its way (Android's `submit`).
    private func cardGuaranteeStep(for current: BookingState, quote: BookingQuote) async -> BookingSubmitOutcome? {
        if let pending = guaranteeCurrencyCode {
            guard await awaitUsableCard(currencyCode: pending) else { return .cardGuaranteePending }
            guaranteeCurrencyCode = nil
        }
        let read = await refreshSavedCards()
        guard case let .success(cards) = read else { return .failed(read.apiErrorOrNil) }
        guard SavedCard.usable(in: cards, currencyCode: quote.currencyCode) == nil else { return nil }
        guard current.cardGuaranteeAccepted else { return .cardGuaranteeConsentRequired }

        let started = await savedCardClient.startCapture(
            consentAccepted: current.cardGuaranteeAccepted,
            countryId: current.countryId ?? marketState.countryId
        )
        guard case let .success(setup) = started else { return .failed(started.apiErrorOrNil) }
        guaranteeCurrencyCode = quote.currencyCode
        return .cardGuaranteeNeeded(PaymentSheetPresentation(
            clientSecret: setup.setupIntentClientSecret,
            ephemeralKey: setup.ephemeralKey,
            stripeCustomerId: setup.stripeCustomerId,
            merchantDisplayName: "Cleansia",
            intentKind: .setup
        ))
    }

    /// Called once PaymentSheet saved the card. The card reaches the account through Stripe's webhook,
    /// so the booking waits until it can be read before the cash order is created (`cardGuaranteeStep`);
    /// if it has not landed in time, nothing is booked and the customer slides again, and that slide waits
    /// for the same card. A time that stopped holding while the card was saved waits for no card: `submit`
    /// refuses it before anything is read.
    func submitAfterCardGuarantee() async -> BookingSubmitOutcome {
        guard guaranteeCurrencyCode != nil else { return .failed(nil) }
        return await submit()
    }

    /// A cancelled or failed setup sheet saved nothing, so the next cash booking captures afresh.
    func abandonCardGuarantee() {
        guaranteeCurrencyCode = nil
    }

    private func awaitUsableCard(currencyCode: String) async -> Bool {
        for read in 0 ..< Self.cardCaptureReads {
            if read > 0 {
                await pauseBetweenCardReads()
            }
            if case let .success(cards) = await refreshSavedCards(),
               SavedCard.usable(in: cards, currencyCode: currencyCode) != nil
            {
                return true
            }
        }
        return false
    }
}
