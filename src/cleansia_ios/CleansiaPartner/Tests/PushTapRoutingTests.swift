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

    func testOrderTapResolvesDetailRouteForOrdersPath() {
        let plan = PushTapRouting.plan(for: .order(orderId: "ord-7"))
        let route = PushTapRouting.deepLinkRoute(plan.orderId)
        XCTAssertEqual(route, .detail(orderId: "ord-7"), "the tap drills to the specific order detail")
    }

    func testOrdersTabTapResolvesNoDetailRoute() {
        let plan = PushTapRouting.plan(for: .ordersTab)
        let route = PushTapRouting.deepLinkRoute(plan.orderId)
        XCTAssertNil(route, "order.new_available lands on the list with no detail push")
    }

    func testInvoiceDestinationSelectsEarningsTabWithItsInvoiceId() {
        let plan = PushTapRouting.plan(for: .invoice(invoiceId: "inv-42"))
        XCTAssertTrue(plan.selectEarningsTab, "payroll.invoice_paid must land on the Earnings tab")
        XCTAssertFalse(plan.selectOrdersTab, "the invoice tap must not select the Orders tab")
        XCTAssertEqual(
            plan.invoiceId,
            "inv-42",
            "the resolved invoice id must reach the earnings deep-link, not be dropped"
        )
        XCTAssertNil(plan.orderId)
    }

    func testInvoiceAndOrderPlansDiffer() {
        let invoice = PushTapRouting.plan(for: .invoice(invoiceId: "inv-1"))
        let order = PushTapRouting.plan(for: .order(orderId: "ord-1"))
        XCTAssertNotEqual(invoice, order)
    }

    func testEarningsTabDestinationSelectsEarningsWithNoInvoiceId() {
        // The invoiceId-less fallback (Android parity) lands on the Earnings
        // summary tab without driving the invoice-detail deep-link.
        let plan = PushTapRouting.plan(for: .earningsTab)
        XCTAssertTrue(plan.selectEarningsTab)
        XCTAssertFalse(plan.selectOrdersTab)
        XCTAssertNil(plan.invoiceId, "no invoice id → the earnings summary, not a detail push")
    }
}
