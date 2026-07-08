import CleansiaCore
import XCTest
@testable import CleansiaCustomer

final class ShellSnackbarInsetTests: XCTestCase {
    func testShellRootLiftsAboveTheBottomChrome() {
        XCTAssertEqual(ShellSnackbarInset.inset(pathDepth: 0), ShellSnackbarInset.overShellBar)
    }

    func testClearanceClearsTheSystemBarAndTheDockedFab() {
        XCTAssertGreaterThan(ShellSnackbarInset.overShellBar, BookFabMetrics.systemTabBarHeight)
        XCTAssertGreaterThanOrEqual(ShellSnackbarInset.overShellBar, BookFabMetrics.chromeEnvelope)
    }

    func testDockedFabCenterSitsOnTheTabBarTopEdge() {
        XCTAssertEqual(BookFabMetrics.bottomPadding + BookFabMetrics.size / 2, BookFabMetrics.systemTabBarHeight)
    }

    func testDockedFabHalfOverlapsTheBar() {
        XCTAssertEqual(BookFabMetrics.bottomPadding, BookFabMetrics.systemTabBarHeight - BookFabMetrics.size / 2)
        XCTAssertGreaterThan(BookFabMetrics.chromeEnvelope, BookFabMetrics.systemTabBarHeight)
    }

    func testRecomputedInsetIsTheDockedFabTopEdgePlusGap() {
        XCTAssertEqual(ShellSnackbarInset.overShellBar, 89)
    }

    func testPushedChildrenUseTheDefaultInset() {
        XCTAssertEqual(ShellSnackbarInset.inset(pathDepth: 1), SnackbarController.defaultBottomInset)
        XCTAssertEqual(ShellSnackbarInset.inset(pathDepth: 3), SnackbarController.defaultBottomInset)
    }
}
