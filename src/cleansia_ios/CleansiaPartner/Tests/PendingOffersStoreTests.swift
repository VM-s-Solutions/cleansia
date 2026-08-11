import CleansiaCore
import CleansiaPartnerApi
import Foundation
import XCTest
@testable import CleansiaPartner

/// The cleaner-facing half of ADR-0045: what is reserved for this cleaner right now, and the one write
/// that refuses it. Both endpoints have been on the partner mobile host since the ADR landed and no iOS
/// client called either, so everything here is a first caller.
@MainActor
final class PendingOffersStoreTests: XCTestCase {
    private var client: FakePartnerOrderClient!
    private var ordersStaleness: OrdersStaleness!

    override func setUp() async throws {
        client = FakePartnerOrderClient()
        ordersStaleness = OrdersStaleness()
    }

    private func makeStore() -> PendingOffersStore {
        PendingOffersStore(client: client, ordersStaleness: ordersStaleness)
    }

    func testASuccessfulFetchPublishesTheServersRowsAndMarksTheSurfaceFresh() async {
        let store = makeStore()
        XCTAssertTrue(store.isStale, "a store that has never fetched must be stale")
        client.pendingOffersResult = .success([.sample(id: "a"), .sample(id: "b")])

        let result = await store.refresh()

        XCTAssertNotNil(try? result.get())
        XCTAssertEqual(store.offers.map(\.id), ["a", "b"])
        XCTAssertFalse(store.isStale)
    }

    func testAFailedFetchNeitherPublishesNorClaimsFreshness() async {
        let store = makeStore()
        client.pendingOffersResult = .failure(ApiError(httpStatus: 500))

        let result = await store.refresh()

        XCTAssertNotNil(result.apiErrorOrNil)
        XCTAssertTrue(store.offers.isEmpty)
        XCTAssertTrue(store.isStale, "a transient failure must not pretend the cache is warm")
    }

    /// Asserts the id reaches the client seam. Every field on the generated `DeclinePreferredOfferCommand`
    /// is optional with a `= nil` default, so an omitted mapping compiles and the wire carries no order
    /// id — a decline that refuses nothing and is reported as a success.
    func testDecliningSendsTheOrderIdToTheClient() async {
        let store = makeStore()

        _ = await store.decline(orderId: "order-1")

        XCTAssertEqual(client.pendingOfferCommands.map(\.name), ["declinePreferredOffer"])
        XCTAssertEqual(client.pendingOfferCommands.first?.orderId, "order-1")
    }

    func testASuccessfulDeclineDropsThatOfferAndLeavesTheOthers() async {
        let store = makeStore()
        client.pendingOffersResult = .success([.sample(id: "a"), .sample(id: "b"), .sample(id: "c")])
        _ = await store.refresh()

        let result = await store.decline(orderId: "b")

        XCTAssertNotNil(try? result.get())
        XCTAssertEqual(store.offers.map(\.id), ["a", "c"])
    }

    /// The order is back with the whole board the instant the hold ends, so the Available pane is wrong
    /// until it refetches.
    func testASuccessfulDeclineRestalesTheBoardAndTheOrder() async {
        let store = makeStore()
        ordersStaleness.markPaneFresh(.available)
        ordersStaleness.markPaneFresh(.active)
        ordersStaleness.markOrderFresh("b")

        _ = await store.decline(orderId: "b")

        XCTAssertTrue(ordersStaleness.isPaneStale(.available))
        XCTAssertTrue(ordersStaleness.isOrderStale("b"))
        XCTAssertFalse(ordersStaleness.isPaneStale(.active), "the cleaner's own jobs did not change")
    }

    func testARefusedDeclineLeavesTheOfferWhereItWas() async {
        let store = makeStore()
        client.pendingOffersResult = .success([.sample(id: "a"), .sample(id: "b")])
        _ = await store.refresh()
        client.declineResult = .failure(ApiError(code: "order.not_found", httpStatus: 404))

        let result = await store.decline(orderId: "b")

        XCTAssertNotNil(result.apiErrorOrNil)
        XCTAssertEqual(store.offers.map(\.id), ["a", "b"])
    }

    func testSignOutDropsTheOffersAndRestalesTheSurface() async {
        let store = makeStore()
        client.pendingOffersResult = .success([.sample(id: "a")])
        _ = await store.refresh()
        XCTAssertFalse(store.isStale)

        await store.clear()

        XCTAssertTrue(store.offers.isEmpty)
        XCTAssertTrue(store.isStale)
    }
}
