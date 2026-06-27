import XCTest
@testable import CleansiaPartner

final class OrderStatusLabelTests: XCTestCase {
    func testPrettifyInsertsSpaceAtCamelBoundaryAndLowercases() {
        XCTAssertEqual(OrderStatusLabel.prettify("OnTheWay"), "On the way")
        XCTAssertEqual(OrderStatusLabel.prettify("InProgress"), "In progress")
    }

    func testPrettifyLeavesSingleWordCapitalized() {
        XCTAssertEqual(OrderStatusLabel.prettify("Completed"), "Completed")
        XCTAssertEqual(OrderStatusLabel.prettify("New"), "New")
    }

    func testLabelPrefersNameWhenPresent() {
        XCTAssertEqual(OrderStatusLabel.label(name: "OnTheWay", value: 3), "On the way")
    }

    func testLabelFallsBackToValueWhenNameMissing() {
        // value 5 → Completed (localized en)
        XCTAssertEqual(OrderStatusLabel.label(name: nil, value: 5), "Completed")
        XCTAssertEqual(OrderStatusLabel.label(name: "  ", value: 2), "Confirmed")
    }

    func testLabelDashWhenNameAndValueMissing() {
        XCTAssertEqual(OrderStatusLabel.label(name: nil, value: nil), "—")
    }
}
