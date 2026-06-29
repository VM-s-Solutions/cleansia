import CleansiaCustomerApi
import Foundation

enum PaymentMethod: String, Equatable, CaseIterable {
    case cash
    case card

    var paymentType: PaymentType {
        switch self {
        case .cash: ._1
        case .card: ._2
        }
    }
}
