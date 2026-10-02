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
        XCTAssertEqual(ShellSnackbarInset.overShellBar, 94)
    }

    func testPrimaryFabIsLargerThanASecondaryDisc() {
        XCTAssertGreaterThanOrEqual(BookFabMetrics.size, 64)
    }

    func testPrimaryFabClearsAdjacentTabIconsOnNarrowestDevice() {
        let narrowestWidth: CGFloat = 375
        let slotSpacing = narrowestWidth * 0.2
        let gapToAdjacentSlotCenter = slotSpacing - BookFabMetrics.size / 2
        let tabIconHalfWidth: CGFloat = 15
        XCTAssertGreaterThan(gapToAdjacentSlotCenter, tabIconHalfWidth)
    }

    /// The system bar already insets each tab root by its own height; the FAB's overhang above it is
    /// what the tab roots must add, plus a gap, or the last item scrolls to a stop under the disc.
    func testTabRootScrollClearanceCoversTheFabOverhangPlusAGap() {
        let overhang = BookFabMetrics.chromeEnvelope - BookFabMetrics.systemTabBarHeight
        XCTAssertEqual(BookFabMetrics.scrollClearance, overhang + Spacing.s)
        XCTAssertEqual(BookFabMetrics.scrollClearance, 45)
    }

    /// Every tab root carries the clearance at the one place they are built, so a fifth tab cannot
    /// silently skip it; the Book placeholder (a blank slot) needs none.
    func testEveryTabRootReservesTheFabClearance() throws {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(
            contentsOf: root.appendingPathComponent("CleansiaCustomer/Sources/Features/Shell/CustomerShellView.swift"),
            encoding: .utf8
        )
        let content = source.components(separatedBy: .whitespacesAndNewlines).joined()
        for tab in ["home", "orders", "rewards", "profile"] {
            XCTAssertTrue(
                content.contains(").bookFabClearance().tabItem{tabLabel(.\(tab))}"),
                "the \(tab) tab root does not reserve the FAB clearance"
            )
        }
        XCTAssertEqual(content.components(separatedBy: ".bookFabClearance()").count - 1, 4)
    }

    func testPushedChildrenUseTheDefaultInset() {
        XCTAssertEqual(ShellSnackbarInset.inset(pathDepth: 1), SnackbarController.defaultBottomInset)
        XCTAssertEqual(ShellSnackbarInset.inset(pathDepth: 3), SnackbarController.defaultBottomInset)
    }
}
