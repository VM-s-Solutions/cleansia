import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// The rows the board last loaded, for the job detail to read by order id: the Available page and the cleaner's
/// own Active rows. A list is nil until the board has loaded it whole; the session's end clears both.
@MainActor
final class OrdersBoardStore: ObservableObject, SessionScopedCache {
    @Published private(set) var available: [OrderListItem]?
    @Published private(set) var own: [OrderListItem]?
    /// The server's count of open jobs, which the Available badge is counted from: it counts the pages not loaded.
    @Published private(set) var availableTotal: Int?

    func publish(available rows: [OrderListItem], total: Int? = nil) {
        available = rows
        availableTotal = total
    }

    func publish(own rows: [OrderListItem]) {
        own = rows
    }

    func row(for orderId: String) -> OrderListItem? {
        (own ?? []).first { $0.id == orderId } ?? (available ?? []).first { $0.id == orderId }
    }

    func clear() async {
        available = nil
        availableTotal = nil
        own = nil
    }
}
