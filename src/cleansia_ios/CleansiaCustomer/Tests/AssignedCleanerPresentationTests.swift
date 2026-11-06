import XCTest
@testable import CleansiaCustomer

final class AssignedCleanerPresentationTests: XCTestCase {
    func testInitialsUseTheFirstAndLastVisibleNameComponents() {
        XCTAssertEqual(AssignedCleanerRow.initials(for: "Jana Nováková"), "JN")
        XCTAssertEqual(AssignedCleanerRow.initials(for: "  Mary Jane Watson  "), "MW")
        XCTAssertEqual(AssignedCleanerRow.initials(for: "Jana"), "J")
    }

    func testInitialsNeverInventAnUnmaskedName() {
        XCTAssertEqual(AssignedCleanerRow.initials(for: "J*** N***"), "JN")
        XCTAssertEqual(AssignedCleanerRow.initials(for: "*** ***"), "?")
        XCTAssertEqual(AssignedCleanerRow.initials(for: nil), "?")
        XCTAssertEqual(AssignedCleanerRow.initials(for: " "), "?")
    }
}
