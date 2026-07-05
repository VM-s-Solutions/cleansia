import CleansiaCore
import Foundation

extension BookingViewModel {
    func submit() async -> BookingSubmitOutcome {
        guard !submitState.isSubmitting else { return .failed }
        submitState = .submitting
        defer { submitState = .idle }

        guard tokenStore.current() != nil else { return .failed }

        guard case let .success(profile) = await profileClient.currentProfile() else {
            return .failed
        }
        guard profile.isComplete else { return .profileIncomplete }

        let current = state
        guard let instant = current.selectedInstant else { return .failed }

        guard let quote = await resolvedQuote(for: current) else { return .failed }

        let countryId = current.savedAddressId == nil
            ? await countryResolver.countryId(forIsoCode: current.countryIsoCode)
            : nil

        let promoIsValid = if case .valid = promoState { true } else { false }
        let command = BookingOrderCommandFactory.make(
            state: current,
            resolved: ResolvedOrderInputs(
                profile: profile,
                quote: quote,
                instant: instant,
                countryId: countryId,
                promoIsValid: promoIsValid
            )
        )

        guard case let .success(order) = await orderCreateClient.create(command) else {
            return .failed
        }

        if current.paymentMethod == .card, isCardPaymentAvailable {
            return await cardPending(for: order)
        }
        // Wipe the draft at the success outcome itself, not on the success screen's
        // exit: the VM is session-lived, so a sheet swiped away over the success
        // screen would otherwise re-arm slide-to-pay with the already-submitted
        // draft (a duplicate-order path). Android resets before navigating too.
        // The card path resets in the view when PaymentSheet resolves to success.
        reset()
        return .success(orderId: order.id, confirmationCode: order.confirmationCode)
    }

    private func cardPending(for order: CreatedOrder) async -> BookingSubmitOutcome {
        guard case let .success(intent) = await paymentIntentClient.createPaymentIntent(orderId: order.id),
              !intent.clientSecret.isEmpty
        else {
            return .failed
        }
        return .cardPending(
            orderId: order.id,
            confirmationCode: order.confirmationCode,
            presentation: PaymentSheetPresentation(
                clientSecret: intent.clientSecret,
                ephemeralKey: intent.ephemeralKey,
                stripeCustomerId: intent.stripeCustomerId,
                merchantDisplayName: "Cleansia"
            )
        )
    }

    func resolvedQuote(for current: BookingState) async -> BookingQuote? {
        let request = current.quoteRequest
        if let cached = quoteState.quote, lastQuoteRequest == request {
            return cached
        }
        switch await quoteClient.quote(request) {
        case let .success(quote):
            lastQuoteRequest = request
            quoteState = .quoted(quote)
            return quote
        case .failure:
            return nil
        }
    }

    #if DEBUG
        func refreshQuoteForTest() async {
            _ = await resolvedQuote(for: state)
        }
    #endif
}
