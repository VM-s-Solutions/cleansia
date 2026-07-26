import CleansiaCore
import Foundation

extension BookingViewModel {
    func submit() async -> BookingSubmitOutcome {
        guard !submitState.isSubmitting else { return .failed(nil) }
        submitState = .submitting
        defer { submitState = .idle }

        guard tokenStore.current() != nil else { return .failed(nil) }

        let profileResult = await profileClient.currentProfile()
        guard case let .success(profile) = profileResult else {
            return .failed(profileResult.apiErrorOrNil)
        }
        guard profile.isComplete else { return .profileIncomplete }

        let current = state
        guard let instant = current.selectedInstant else { return .failed(nil) }

        let quoteResult = await resolvedQuote(for: current)
        guard case let .success(quote) = quoteResult else {
            return .failed(quoteResult.apiErrorOrNil)
        }

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

        // The one that matters most: `order.no_available_spots`,
        // `order.time_conflict`, `order.total_price.not_match` and
        // `city.not_serviced` are all rejections the customer can act on, and
        // every one of them already ships translated in five locales.
        let createResult = await orderCreateClient.create(command)
        guard case let .success(order) = createResult else {
            return .failed(createResult.apiErrorOrNil)
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
        // The order already exists here, so the customer is owed the real reason
        // the card step stopped. An empty secret on a 200 has no error to report
        // and correctly falls back to `nil`.
        let intentResult = await paymentIntentClient.createPaymentIntent(orderId: order.id)
        guard case let .success(intent) = intentResult, !intent.clientSecret.isEmpty else {
            return .failed(intentResult.apiErrorOrNil)
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

    /// Returns the whole `ApiResult` rather than an optional: flattening it to
    /// `BookingQuote?` discarded a populated `ApiError` one frame before
    /// `submit` could hand it to the sheet.
    func resolvedQuote(for current: BookingState) async -> ApiResult<BookingQuote> {
        let request = current.quoteRequest
        if let cached = quoteState.quote, lastQuoteRequest == request {
            return .success(cached)
        }
        let result = await quoteClient.quote(request)
        if case let .success(quote) = result {
            lastQuoteRequest = request
            quoteState = .quoted(quote)
        }
        return result
    }

    #if DEBUG
        func refreshQuoteForTest() async {
            _ = await resolvedQuote(for: state)
        }
    #endif
}
