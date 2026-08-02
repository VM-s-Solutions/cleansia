import SwiftUI
import XCTest
@testable import CleansiaCore

/// The sheet-edge ornament geometry and the non-drag ways of reaching an anchor.
///
/// A drag is unreachable for VoiceOver and undiscoverable for everyone else, so the
/// handle also carries a tap and an adjustable action; both resolve through pure
/// functions here rather than inside the gesture closure.
final class SnapSheetOrnamentTests: XCTestCase {
    func testOrnamentCentreSitsOnTheSheetEdge() {
        // Android anchors the puck at `sheetTopPx - sizePx / 2` so half of it
        // overlaps the map and half the sheet (FloatingMascot.kt:61).
        XCTAssertEqual(SnapSheetOrnament.offsetY(sheetTop: 200, size: 128), 136)
        XCTAssertEqual(SnapSheetOrnament.offsetY(sheetTop: 0, size: 128), -64)
    }

    func testOrnamentFollowsTheEdgeAsTheSheetMoves() {
        let peekTop = SnapResolver.sheetTop(for: .peek, containerHeight: 800)
        let mapFocusTop = SnapResolver.sheetTop(for: .mapFocus, containerHeight: 800)
        let travel = SnapSheetOrnament.offsetY(sheetTop: mapFocusTop, size: 128)
            - SnapSheetOrnament.offsetY(sheetTop: peekTop, size: 128)
        XCTAssertEqual(travel, mapFocusTop - peekTop, accuracy: 0.001)
    }

    func testTapFromPeekRevealsTheMap() {
        XCTAssertEqual(SnapAnchor.peek.tapToggled, .mapFocus)
    }

    func testTapFromExpandedRevealsTheMap() {
        XCTAssertEqual(SnapAnchor.expanded.tapToggled, .mapFocus)
    }

    func testTapFromMapFocusReturnsToPeek() {
        XCTAssertEqual(SnapAnchor.mapFocus.tapToggled, .peek)
    }

    func testTapIsAlwaysReversible() {
        for anchor in SnapAnchor.allCases {
            XCTAssertNotEqual(anchor.tapToggled, anchor)
            XCTAssertEqual(anchor.tapToggled.tapToggled, anchor == .expanded ? .peek : anchor)
        }
    }

    func testAdjustableActionWalksTheAnchorsInCoverageOrder() {
        XCTAssertEqual(SnapAnchor.mapFocus.moreExpanded, .peek)
        XCTAssertEqual(SnapAnchor.peek.moreExpanded, .expanded)
        XCTAssertEqual(SnapAnchor.expanded.lessExpanded, .peek)
        XCTAssertEqual(SnapAnchor.peek.lessExpanded, .mapFocus)
    }

    func testAdjustableActionSaturatesAtBothEnds() {
        XCTAssertEqual(SnapAnchor.expanded.moreExpanded, .expanded)
        XCTAssertEqual(SnapAnchor.mapFocus.lessExpanded, .mapFocus)
    }

    func testEveryAnchorAnnouncesADistinctValue() {
        let values = SnapAnchor.allCases.map(\.accessibilityValue)
        XCTAssertEqual(Set(values).count, SnapAnchor.allCases.count)
        XCTAssertFalse(values.contains(where: \.isEmpty))
    }
}
