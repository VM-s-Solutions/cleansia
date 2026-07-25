import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// A SERVER-started Live Activity is the whole point of push-to-start, but the card's attributes carry
/// only `orderNumber` (ADR-0029 D4 / S6 keeps the internal id off the APNs payload) while
/// `POST /api/LiveActivity/Register` is keyed by order id. This resolver is the mapping in between; if it
/// answers wrong the backend registers an update token against the wrong order — or none at all, which is
/// the "card appears and then freezes" symptom.
final class LiveActivityOrderResolverTests: XCTestCase {
    private func page(_ items: [OrderListItem]) -> OrdersPage {
        OrdersPage(items: items, total: items.count)
    }

    private func listItem(id: String, orderNumber: String?) -> OrderListItem {
        OrderListItem(id: id, displayOrderNumber: orderNumber)
    }

    func testResolvesTheIdOfTheMatchingOrderNumber() async {
        let client = FakeOrderClient()
        client.pages = [page([
            listItem(id: "order-a", orderNumber: "1001"),
            listItem(id: "order-b", orderNumber: "1002")
        ])]

        let resolved = await CustomerLiveActivityOrderResolver(client: client)
            .orderId(forOrderNumber: "1002")

        XCTAssertEqual(resolved, "order-b")
    }

    func testReturnsNilWhenNoOrderCarriesThatNumber() async {
        let client = FakeOrderClient()
        client.pages = [page([listItem(id: "order-a", orderNumber: "1001")])]

        let resolved = await CustomerLiveActivityOrderResolver(client: client)
            .orderId(forOrderNumber: "9999")

        XCTAssertNil(resolved)
    }

    func testReturnsNilOnApiFailureRatherThanGuessing() async {
        // A background launch races the network coming up. Nil here means "ask again", and the
        // coordinator's bounded retry is what acts on it — it must never be read as "no such order".
        let client = FakeOrderClient()
        client.pageError = ApiError(httpStatus: 401)

        let resolved = await CustomerLiveActivityOrderResolver(client: client)
            .orderId(forOrderNumber: "1001")

        XCTAssertNil(resolved)
    }

    func testEmptyOrderNumberNeverHitsTheNetwork() async {
        // An empty number would match any order whose displayOrderNumber is also blank, binding a token
        // to an unrelated order. Refuse before the call.
        let client = FakeOrderClient()
        client.pages = [page([listItem(id: "order-a", orderNumber: nil)])]

        let resolved = await CustomerLiveActivityOrderResolver(client: client)
            .orderId(forOrderNumber: "")

        XCTAssertNil(resolved)
        XCTAssertTrue(client.pageRequests.isEmpty)
    }

    func testReadsOnlyThePageContainingTheRecentOrders() async {
        let client = FakeOrderClient()
        client.pages = [page([listItem(id: "order-a", orderNumber: "1001")])]

        _ = await CustomerLiveActivityOrderResolver(client: client, pageSize: 20)
            .orderId(forOrderNumber: "1001")

        XCTAssertEqual(client.pageRequests.count, 1)
        XCTAssertEqual(client.pageRequests.first?.offset, 0)
        XCTAssertEqual(client.pageRequests.first?.limit, 20)
    }
}
