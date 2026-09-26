import CleansiaCore
import Combine
import Foundation

extension BookingViewModel {
    /// Read from the live session and the crew the server quoted for the selection on screen; a quote
    /// for an earlier selection says nothing about this one.
    var cashEligibility: CashEligibility {
        CashEligibility.resolve(signedIn: tokenStore.current() != nil, requiredEmployees: quotedRequiredEmployees)
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

    /// Cash goes out only on a fresh quote that allows it; otherwise the choice is taken away and the
    /// caller sends nothing.
    func takeCashAwayIfRefused(_ paymentMethod: PaymentMethod, by quote: BookingQuote) -> Bool {
        guard paymentMethod == .cash,
              CashEligibility.resolve(signedIn: true, requiredEmployees: quote.requiredEmployees) != .available
        else { return false }
        dropCash(announce: true)
        return true
    }

    /// The server's refusal is already a sentence the customer reads, so the choice goes without a second.
    func takeCashAwayIfServerRefused(_ error: ApiError?) {
        if error?.code == CashEligibility.refusalCode {
            dropCash(announce: false)
        }
    }

    func landQuote(_ quote: BookingQuote, for request: QuoteRequest) {
        lastQuoteRequest = request
        quoteState = .quoted(quote)
        if state.paymentMethod == .cash, cashEligibility.refusesCash {
            dropCash(announce: true)
        }
    }
}
