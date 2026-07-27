import CleansiaPartnerApi
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

    func testLabelUsesLocalizedStatusForKnownValueIgnoringWireName() {
        // A known numeric status wins over the raw wire name so a non-localized
        // backend name ("OnTheWay") never leaks into a translated build.
        XCTAssertEqual(OrderStatusLabel.label(track(name: "OnTheWay", value: 3)), L10n.Orders.statusLabel(._3))
        XCTAssertEqual(OrderStatusLabel.label(track(name: "ZZZ", value: 4)), L10n.Orders.statusLabel(._4))
    }

    func testLabelMapsEachKnownValueToLocalizedLabel() {
        XCTAssertEqual(OrderStatusLabel.label(track(name: nil, value: 5)), L10n.Orders.statusLabel(._5))
        XCTAssertEqual(OrderStatusLabel.label(track(name: "  ", value: 2)), L10n.Orders.statusLabel(._2))
    }

    func testLabelForUnknownValueUsesPrettifiedNameInDebugElseDash() {
        let result = OrderStatusLabel.label(track(name: "FutureStatus", value: 99))
        #if DEBUG
            XCTAssertEqual(result, "Future status")
        #else
            XCTAssertEqual(result, "—")
        #endif
    }

    func testLabelDashWhenNameAndValueMissing() {
        XCTAssertEqual(OrderStatusLabel.label(track(name: nil, value: nil)), "—")
        XCTAssertEqual(OrderStatusLabel.label(OrderStatusTrackDto()), "—")
    }

    private func track(name: String?, value: Int?) -> OrderStatusTrackDto {
        OrderStatusTrackDto(status: Code(name: name, value: value))
    }
}
