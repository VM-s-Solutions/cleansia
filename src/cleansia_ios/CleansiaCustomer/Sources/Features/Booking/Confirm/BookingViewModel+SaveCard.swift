import CleansiaCore
import Foundation

extension BookingViewModel {
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
}
