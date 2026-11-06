import CleansiaCore
import Combine
import Foundation

extension BookingViewModel {
    /// The currency every wizard amount is labelled with. The quote's own code the moment one lands;
    /// until then the catalogue's default, which is what the pre-quote catalogue prices are stated in.
    /// Nil only before the catalogue has loaded, when there is no figure on screen to label.
    var displayCurrencyCode: String? {
        quoteState.quote?.currencyCode ?? catalogState.loadedValue?.currencyCode
    }

    /// Read from the live session and the crew the server quoted for the selection on screen; a quote
    /// for an earlier selection says nothing about this one.
    var cashEligibility: CashEligibility {
        let eligibility = CashEligibility.resolve(
            signedIn: tokenStore.current() != nil,
            requiredEmployees: quotedRequiredEmployees,
            hasUsableSavedCard: usableCashCard
        )
        return eligibility == .pending && cashCardReadFailed ? .cardCheckFailed : eligibility
    }

    private var currentCashSession: SavedCardSessionIdentity? {
        guard let token = tokenStore.current()?.accessToken,
              let userId = JwtDecoder.userId(of: token) else { return nil }
        return SavedCardSessionIdentity(userId: userId, generation: cashSessionGeneration())
    }

    private var usableCashCard: Bool? {
        guard cashCardsSession == currentCashSession, let cards = cashCards,
              let currency = displayCurrencyCode else { return nil }
        return SavedCard.usable(in: cards, currencyCode: currency) != nil
    }

    /// Read the authenticated server list; pending SetupIntents never enter it. The list's currency
    /// and expiry decide the visible option, and submit always reads it again before creating anything.
    @discardableResult
    func refreshCashCard() async -> ApiResult<[SavedCard]> {
        cashCardReadSequence += 1
        let sequence = cashCardReadSequence
        let session = currentCashSession
        cashCards = nil
        cashCardReadFailed = false
        cashCardsSession = nil
        guard session != nil else { return .success([]) }
        let result = await savedCardClient.myCards()
        guard sequence == cashCardReadSequence else {
            return .failure(ApiError(code: ApiError.cancelledCode))
        }
        guard session == currentCashSession else {
            cashCardReadFailed = tokenStore.current() != nil
            return .failure(ApiError(code: ApiError.cancelledCode))
        }
        switch result {
        case let .success(cards):
            cashCards = cards
            cashCardsSession = session
            if state.paymentMethod == .cash, cashEligibility.refusesCash { dropCash(announce: true) }
        case .failure:
            cashCardReadFailed = true
        }
        return result
    }

    private var quotedRequiredEmployees: Int? {
        guard case let .quoted(quote) = quoteState,
              lastQuoteRequest == state.quoteRequest(marketCountryId: marketState.countryId)
        else { return nil }
        return quote.requiredEmployees
    }

    func selectPayment(_ method: PaymentMethod) {
        if method == .cash, cashEligibility != .available { return }
        cashCleared = false
        update { current in
            var next = current
            next.paymentMethod = method
            return next
        }
    }

    func dropCash(announce: Bool) {
        guard state.paymentMethod == .cash else { return }
        update { current in
            var next = current
            next.paymentMethod = nil
            return next
        }
        cashCleared = true
        if announce { events.send(.cashCleared) }
    }

    func cashRefusalBeforeCreate(_ paymentMethod: PaymentMethod, quote: BookingQuote) async -> BookingSubmitOutcome? {
        guard paymentMethod == .cash else { return nil }
        guard quote.requiredEmployees == 1 else {
            dropCash(announce: true)
            return .paymentMethodCleared
        }
        let cards = await refreshCashCard()
        guard case .success = cards else { return .failed(cards.apiErrorOrNil) }
        return takeCashAwayIfRefused(paymentMethod, by: quote) ? .paymentMethodCleared : nil
    }

    /// Cash goes out only on a fresh quote that allows it; otherwise the choice is taken away and the
    /// caller sends nothing.
    func takeCashAwayIfRefused(_ paymentMethod: PaymentMethod, by quote: BookingQuote) -> Bool {
        guard paymentMethod == .cash,
              CashEligibility.resolve(
                  signedIn: true, requiredEmployees: quote.requiredEmployees,
                  hasUsableSavedCard: cashCards
                      .map { SavedCard.usable(in: $0, currencyCode: quote.currencyCode) != nil }
              ) != .available
        else { return false }
        dropCash(announce: true)
        return true
    }

    /// The server's refusal is already a sentence the customer reads, so the choice goes without a second.
    func takeCashAwayIfServerRefused(_ error: ApiError?) {
        if error?.code == CashEligibility.refusalCode || error?.code == "order.cash_requires_saved_card" {
            dropCash(announce: false)
            cashCardReadSequence += 1
            cashCards = nil
            cashCardsSession = nil
            cashCardReadFailed = false
            Task { [weak self] in await self?.refreshCashCard() }
        }
    }

    func landQuote(_ quote: BookingQuote, for request: QuoteRequest, at now: Date = Date()) {
        lastQuoteRequest = request
        quotedAt = now
        quoteState = .quoted(quote)
        if state.paymentMethod == .cash, cashEligibility.refusesCash {
            dropCash(announce: true)
        }
    }
}

/// Credential rotation keeps this account identity; logout/login clears the registered capture store
/// and advances its epoch, so an earlier session cannot lend its card list to a later one.
struct SavedCardSessionIdentity: Equatable {
    let userId: String
    let generation: UInt
}
