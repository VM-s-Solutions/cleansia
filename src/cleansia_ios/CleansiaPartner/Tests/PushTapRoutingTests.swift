import XCTest
@testable import CleansiaPartner

final class PushTapRoutingTests: XCTestCase {
    func testOrderDestinationDrivesDetailDrillWithItsOrderId() {
        let plan = PushTapRouting.plan(for: .order(orderId: "ord-7"))
        XCTAssertTrue(plan.selectOrdersTab)
        XCTAssertEqual(plan.orderId, "ord-7", "the resolved order id must reach the detail push, not be dropped")
    }

    func testOrdersTabDestinationLandsOnListWithoutDetailPush() {
        let plan = PushTapRouting.plan(for: .ordersTab)
        XCTAssertTrue(plan.selectOrdersTab)
        XCTAssertNil(plan.orderId, "order.new_available lands on the list, no specific order")
    }

    func testOrderAndOrdersTabPlansDiffer() {
        let order = PushTapRouting.plan(for: .order(orderId: "ord-1"))
        let tab = PushTapRouting.plan(for: .ordersTab)
        XCTAssertNotEqual(order, tab)
    }

    func testOrderTapAppendsDetailRouteToOrdersPath() {
        let plan = PushTapRouting.plan(for: .order(orderId: "ord-7"))
        let path = PushTapRouting.appendingDeepLink(plan.orderId, to: [])
        XCTAssertEqual(path, [.detail(orderId: "ord-7")], "the tap drills to the specific order detail")
    }

    func testOrdersTabTapDoesNotAppendDetailRoute() {
        let plan = PushTapRouting.plan(for: .ordersTab)
        let path = PushTapRouting.appendingDeepLink(plan.orderId, to: [])
        XCTAssertEqual(path, [], "order.new_available lands on the list with no detail push")
    }
}
