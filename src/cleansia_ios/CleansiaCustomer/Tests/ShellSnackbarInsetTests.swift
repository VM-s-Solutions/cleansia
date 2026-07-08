import CleansiaCore
import XCTest
@testable import CleansiaCustomer

final class ShellSnackbarInsetTests: XCTestCase {
    func testShellRootLiftsAboveTheBottomChrome() {
        XCTAssertEqual(ShellSnackbarInset.inset(pathDepth: 0), ShellSnackbarInset.overShellBar)
    }

    func testClearanceClearsTheSystemBarAndTheFloatingFab() {
        XCTAssertGreaterThan(ShellSnackbarInset.overShellBar, BookFabMetrics.systemTabBarHeight)
        XCTAssertGreaterThanOrEqual(ShellSnackbarInset.overShellBar, BookFabMetrics.chromeEnvelope)
    }

    func testRecomputedInsetIsTheBarPlusFabEnvelopePlusGap() {
        XCTAssertEqual(ShellSnackbarInset.overShellBar, 129)
    }

    func testPushedChildrenUseTheDefaultInset() {
        XCTAssertEqual(ShellSnackbarInset.inset(pathDepth: 1), SnackbarController.defaultBottomInset)
        XCTAssertEqual(ShellSnackbarInset.inset(pathDepth: 3), SnackbarController.defaultBottomInset)
    }
}
