import CleansiaCore
import Foundation

enum CustomerBookingTokenStore {
    static let shared: TokenStore = KeychainTokenStore(service: "cz.cleansia.customer.tokens")
}
