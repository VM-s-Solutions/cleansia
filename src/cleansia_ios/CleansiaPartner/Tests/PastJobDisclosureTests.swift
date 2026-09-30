import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// A partner keeps the customer's name, address and phone while the job is live and for 24 hours after
/// completion; a cancellation closes them at once. After that the server sends its crew the browsing
/// shape, and the detail has to say the details were removed rather than promise them "once you take
/// the order".
final class PastJobDisclosureTests: XCTestCase {
    private let street = OrderAddress(street: "Korunní 810/104", city: "Praha", zipCode: "12000")

    private func crewOrder(status: Int, address: OrderAddress?) throws -> OrderDetail {
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: status)
        item.isAssignedToCurrentUser = true
        item.address = address
        item.customerName = address == nil ? "" : "Jana Nováková"
        item.customerPhone = address == nil ? "" : "+420600123456"
        item.customerAddressApproximate = "Praha · 120"
        return try OrderDetail(item)
    }

    func testACompletedJobPastItsWindowSaysTheDetailsWent24HoursAfterCompletion() throws {
        XCTAssertEqual(
            try crewOrder(status: 5, address: nil).customerDetailsClosedNote,
            L10n.Orders.customerDetailsClosedCompleted
        )
    }

    func testACancelledJobSaysTheDetailsWentWithTheCancellation() throws {
        XCTAssertEqual(
            try crewOrder(status: 6, address: nil).customerDetailsClosedNote,
            L10n.Orders.customerDetailsClosedCancelled
        )
    }

    func testTheTwoNotesAreDistinctTranslatedSentences() {
        XCTAssertNotEqual(L10n.Orders.customerDetailsClosedCompleted, "order_customer_details_closed_completed")
        XCTAssertNotEqual(L10n.Orders.customerDetailsClosedCancelled, "order_customer_details_closed_cancelled")
        XCTAssertNotEqual(L10n.Orders.customerDetailsClosedCompleted, L10n.Orders.customerDetailsClosedCancelled)
    }

    func testACompletedJobInsideItsWindowStillShowsTheCustomer() throws {
        XCTAssertNil(try crewOrder(status: 5, address: street).customerDetailsClosedNote)
    }

    func testALiveJobIsNeverClosed() throws {
        for status in [2, 3, 4] {
            XCTAssertNil(try crewOrder(status: status, address: street).customerDetailsClosedNote, "status \(status)")
        }
    }

    func testABrowsingPartnerNeverHadTheDetailsSoNothingWasRemoved() throws {
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: 0)
        item.isAssignedToCurrentUser = false
        item.customerName = ""
        item.customerAddressApproximate = "Praha · 120"

        XCTAssertNil(try OrderDetail(item).customerDetailsClosedNote)
    }

    func testAPastJobRowReadsItsOrderNumberNotAGuest() {
        var row = OrderListItem()
        row.displayOrderNumber = "ORD-2026-007"
        row.customerName = ""
        row.customerAddressApproximate = "Praha · 120"

        XCTAssertEqual(OrdersFormat.compactSubtitle(row), "#ORD-2026-007")
    }
}
