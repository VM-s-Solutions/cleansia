import Foundation

/// Resolves the order id for a Live Activity the app did NOT start.
///
/// Why this seam exists: `CleanOrderAttributes` deliberately carries only `orderNumber` and never the
/// internal order id (ADR-0029 D4 / S6 — the attributes ride through APNs into the lock-screen payload).
/// But `POST /api/LiveActivity/Register` is keyed by order id, so a SERVER-started card — the whole point
/// of push-to-start — has to map its one public identity back to an id before its update token can be
/// registered. Resolving it against the customer's own orders keeps the id off the wire, which is exactly
/// what D4 is protecting.
protocol LiveActivityOrderResolving: Sendable {
    func orderId(forOrderNumber orderNumber: String) async -> String?
}

/// Looks the number up in the signed-in user's most recent orders. A server-started activity is by
/// definition for an order that just changed status, so it is on page 0 — this never pages.
struct CustomerLiveActivityOrderResolver: LiveActivityOrderResolving {
    private let client: OrderClient
    private let pageSize: Int

    init(client: OrderClient, pageSize: Int = 20) {
        self.client = client
        self.pageSize = pageSize
    }

    func orderId(forOrderNumber orderNumber: String) async -> String? {
        guard !orderNumber.isEmpty else { return nil }
        guard case let .success(page) = await client.getMyOrders(offset: 0, limit: pageSize) else {
            // No session yet, or the network is not up in the background-launch window. The caller
            // retries; returning nil must never be mistaken for "no such order".
            return nil
        }

        return page.items.first { $0.displayOrderNumber == orderNumber }?.id
    }
}
