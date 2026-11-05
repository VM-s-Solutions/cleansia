import SwiftUI
import XCTest
@testable import CleansiaCore

/// Every row of the status table both apps' pills and timeline points read, by the backend's wire value.
final class OrderStatusStyleTests: XCTestCase {
    func testEveryStatusTakesItsTint() {
        XCTAssertEqual(OrderStatusStyle.tint(wireValue: 0), CleansiaColors.warningStar, "New")
        XCTAssertEqual(OrderStatusStyle.tint(wireValue: 1), CleansiaColors.warningStar, "Pending")
        XCTAssertEqual(OrderStatusStyle.tint(wireValue: 2), CleansiaColors.primary, "Confirmed")
        XCTAssertEqual(OrderStatusStyle.tint(wireValue: 3), CleansiaColors.secondary, "On the way")
        XCTAssertEqual(OrderStatusStyle.tint(wireValue: 4), CleansiaColors.secondary, "In progress")
        XCTAssertEqual(OrderStatusStyle.tint(wireValue: 5), CleansiaColors.successText, "Completed")
        XCTAssertEqual(OrderStatusStyle.tint(wireValue: 6), CleansiaColors.error, "Cancelled")
    }

    func testEveryStatusTakesItsTone() {
        for value in 0 ... 4 {
            XCTAssertEqual(OrderStatusStyle.tone(wireValue: value), .live, "status \(value) is still moving")
        }
        XCTAssertEqual(OrderStatusStyle.tone(wireValue: 5), .done)
        XCTAssertEqual(OrderStatusStyle.tone(wireValue: 6), .failed)
    }

    /// A status the backend adds later, and a missing one, draw the neutral pill with no mark.
    func testAnUnknownOrMissingStatusIsNeutral() {
        for value in [nil, 7, -1] as [Int?] {
            XCTAssertEqual(OrderStatusStyle.tint(wireValue: value), CleansiaColors.outlineVariant)
            XCTAssertEqual(OrderStatusStyle.tone(wireValue: value), .unknown)
        }
    }
}
