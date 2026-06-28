import XCTest
@testable import CleansiaCustomer

@MainActor
final class CustomerShellSelectionTests: XCTestCase {
    func testTabOrderMirrorsAndroidMainTab() {
        XCTAssertEqual(CustomerShellTab.allCases, [.home, .orders, .rewards, .profile])
    }

    func testDefaultSelectionIsHome() {
        XCTAssertEqual(CustomerShellModel().selection, .home)
    }

    func testEachTabHasItsSystemImage() {
        XCTAssertEqual(CustomerShellTab.home.systemImage, "house")
        XCTAssertEqual(CustomerShellTab.orders.systemImage, "doc.text")
        XCTAssertEqual(CustomerShellTab.rewards.systemImage, "gift")
        XCTAssertEqual(CustomerShellTab.profile.systemImage, "person")
    }

    func testEachTabResolvesNonEmptyLabel() {
        for tab in CustomerShellTab.allCases {
            XCTAssertFalse(tab.label.isEmpty)
        }
    }

    func testBookActionIsInertInThisSlice() {
        let model = CustomerShellModel()
        XCTAssertFalse(model.didOpenBooking)
        model.book()
        XCTAssertFalse(model.didOpenBooking)
    }
}
