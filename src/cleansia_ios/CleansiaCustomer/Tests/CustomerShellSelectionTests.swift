import XCTest
@testable import CleansiaCustomer

@MainActor
final class CustomerShellSelectionTests: XCTestCase {
    func testNavigationTabsMirrorAndroidMainTab() {
        XCTAssertEqual(CustomerShellTab.navigationTabs, [.home, .orders, .rewards, .profile])
    }

    func testBookPlaceholderSitsInTheCenterOfTheBarSlots() {
        XCTAssertEqual(CustomerShellTab.allCases, [.home, .orders, .book, .rewards, .profile])
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

    func testBookPlaceholderHasNoLabelOrSystemImage() {
        XCTAssertTrue(CustomerShellTab.book.label.isEmpty)
        XCTAssertTrue(CustomerShellTab.book.systemImage.isEmpty)
    }

    func testEachNavigationTabResolvesNonEmptyLabel() {
        for tab in CustomerShellTab.navigationTabs {
            XCTAssertFalse(tab.label.isEmpty)
        }
    }

    func testBookActionPresentsTheBookingSheet() {
        let model = CustomerShellModel()
        XCTAssertFalse(model.isBookingPresented)
        model.book()
        XCTAssertTrue(model.isBookingPresented)
    }

    func testSelectingTheCenterPlaceholderOpensBookingAndSnapsBack() {
        let model = CustomerShellModel()
        model.selection = .orders
        XCTAssertFalse(model.resolveSelection())

        model.selection = .book
        XCTAssertTrue(model.resolveSelection())
        XCTAssertEqual(model.selection, .orders)
    }

    func testResolvingARealTabNeverOpensBooking() {
        let model = CustomerShellModel()
        model.selection = .rewards
        XCTAssertFalse(model.resolveSelection())
        XCTAssertEqual(model.selection, .rewards)
    }

    func testCenterPlaceholderSnapsBackToTheMostRecentRealTab() {
        let model = CustomerShellModel()
        model.selection = .profile
        _ = model.resolveSelection()

        model.selection = .book
        XCTAssertTrue(model.resolveSelection())
        XCTAssertEqual(model.selection, .profile)
    }
}
