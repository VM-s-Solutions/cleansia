import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

/// The orders reserved for this cleaner alone until their deadline (ADR-0045). Cached rather than
/// fetched per screen because three surfaces read the same answer — the dashboard card, the offers
/// list, and the order detail's "this one is yours until…" disclosure.
@MainActor
final class PendingOffersStore: ObservableObject, SessionScopedCache {
    @Published private(set) var offers: [PendingOfferItem] = []

    private let client: PartnerOrderClient
    private let ordersStaleness: OrdersStaleness
    private let staleness: Staleness

    init(
        client: PartnerOrderClient,
        ordersStaleness: OrdersStaleness,
        staleness: Staleness = Staleness()
    ) {
        self.client = client
        self.ordersStaleness = ordersStaleness
        self.staleness = staleness
    }

    var isStale: Bool {
        staleness.isStale
    }

    func refresh() async -> ApiResult<[PendingOfferItem]> {
        let result = await client.myPendingOffers()
        if case let .success(rows) = result {
            offers = rows
            staleness.markFresh()
        }
        return result
    }

    /// Refuse a reservation. One server-side write — the hold ends now and the order returns to the
    /// whole board — so on success the row leaves the cache and every surface reading it agrees.
    func decline(orderId: String) async -> ApiResult<Void> {
        let result = await client.declinePreferredOffer(orderId: orderId)
        if case .success = result {
            offers.removeAll { $0.id == orderId }
            ordersStaleness.invalidateOrder(orderId)
            ordersStaleness.invalidatePanes(for: .declinePreferredOffer)
        }
        return result
    }

    func offer(forOrderId orderId: String) -> PendingOfferItem? {
        offers.first { $0.id == orderId }
    }

    func clear() async {
        offers = []
        staleness.invalidate()
    }
}
