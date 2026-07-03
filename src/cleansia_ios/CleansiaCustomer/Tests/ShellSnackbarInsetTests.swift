import CleansiaCore
import XCTest
@testable import CleansiaCustomer

final class ShellSnackbarInsetTests: XCTestCase {
    func testShellRootLiftsAboveTheBarComposite() {
        XCTAssertEqual(ShellSnackbarInset.inset(pathDepth: 0), ShellSnackbarInset.overShellBar)
    }

    func testClearanceExceedsTheBarCompositeHeight() {
        XCTAssertGreaterThan(ShellSnackbarInset.overShellBar, 88)
    }

    func testPushedChildrenUseTheDefaultInset() {
        XCTAssertEqual(ShellSnackbarInset.inset(pathDepth: 1), SnackbarController.defaultBottomInset)
        XCTAssertEqual(ShellSnackbarInset.inset(pathDepth: 3), SnackbarController.defaultBottomInset)
    }
}
