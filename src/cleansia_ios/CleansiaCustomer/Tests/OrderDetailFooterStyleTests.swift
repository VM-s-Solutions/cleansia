import CleansiaCore
import XCTest
@testable import CleansiaCustomer

/// The call-site half of the footer's colour contract. Core's
/// `OutlinedButtonColorsTests` asserts the *resolver* — that
/// `CleansiaOutlinedButton` paints both slots with whatever colour it is handed —
/// so it stays green whichever colour this screen picks. These pin the pick.
final class OrderDetailFooterStyleTests: XCTestCase {
    func testReportIssueIsErrorTintedNotBrandPrimary() {
        XCTAssertEqual(OrderDetailFooterStyle.reportIssue.tint, CleansiaColors.error)
        XCTAssertNotEqual(OrderDetailFooterStyle.reportIssue.tint, CleansiaColors.primary)
    }

    /// The reported defect: Report issue rendered in the same brand blue as the
    /// Make recurring row directly above it on a completed order, leaving the two
    /// separable only by reading their labels.
    func testReportIssueNoLongerMatchesTheMakeRecurringRowAboveIt() {
        XCTAssertEqual(OrderDetailFooterStyle.makeRecurring.tint, CleansiaColors.primary)
        XCTAssertNotEqual(
            OrderDetailFooterStyle.reportIssue.tint,
            OrderDetailFooterStyle.makeRecurring.tint
        )
    }

    /// Cancel and Report issue now share one tint, and Confirmed is the single
    /// status that renders both — so on that footer the glyphs are the entire
    /// non-textual differentiator between abandoning a booking and complaining
    /// about one. Collapsing them to the same symbol would be invisible to every
    /// other check.
    func testCancelAndReportIssueShareATintSoTheirGlyphsMustDiffer() {
        XCTAssertTrue(OrderStatusGroup.isCancellable(._2))
        XCTAssertTrue(OrderStatusGroup.isReportable(._2))
        XCTAssertEqual(OrderDetailFooterStyle.cancel.tint, CleansiaColors.error)
        XCTAssertEqual(OrderDetailFooterStyle.reportIssue.tint, OrderDetailFooterStyle.cancel.tint)
        XCTAssertNotEqual(OrderDetailFooterStyle.reportIssue.icon, OrderDetailFooterStyle.cancel.icon)
    }
}
