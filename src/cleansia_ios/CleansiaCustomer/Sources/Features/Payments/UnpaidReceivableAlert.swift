import CleansiaCore
import SwiftUI

extension ApiError {
    static let unpaidReceivableCode = "order.unpaid_receivable"

    /// The customer owes a company money, which refuses every new booking, cash or card. The key can share its
    /// wire code with another whole-booking refusal, so it is looked for among every key.
    var refusesForUnpaidAmount: Bool {
        carries(Self.unpaidReceivableCode)
    }
}

extension View {
    /// The answer to a booking refused for an amount owed: what it means, and the way to Payments, where it is paid.
    func unpaidReceivableAlert(isPresented: Binding<Bool>, onPay: @escaping () -> Void) -> some View {
        alert(L10n.Payments.unpaidTitle, isPresented: isPresented) {
            Button(L10n.Payments.unpaidPay, action: onPay)
            Button(L10n.Booking.close, role: .cancel) {}
        } message: {
            Text(ApiErrorLocalizer().message(for: ApiError(code: ApiError.unpaidReceivableCode)))
        }
    }
}
