import SwiftUI
import XCTest
@testable import CleansiaCore

/// TC-IOS-SNAP (ADR-0021) — the pure snap-offset resolver:
/// `(currentAnchor, dragTranslation, predictedEnd, containerHeight) → SnapAnchor`,
/// resting default `peek` (≈0.75), velocity-aware, clamped at the ends, never
/// hidden/dismissed (the Android `skipHiddenState=true` parity).
final class SnapSheetSnapResolutionTests: XCTestCase {
    private let height: CGFloat = 800

    private func resolve(
        from anchor: SnapAnchor,
        drag: CGFloat,
        predicted: CGFloat
    ) -> SnapAnchor {
        SnapResolver.resolve(
            from: anchor,
            dragTranslation: drag,
            predictedEndTranslation: predicted,
            containerHeight: height
        )
    }

    func testThreeAnchorsAreOrderedByCoverage() {
        XCTAssertLessThan(SnapAnchor.mapFocus.coveredFraction, SnapAnchor.peek.coveredFraction)
        XCTAssertLessThan(SnapAnchor.peek.coveredFraction, SnapAnchor.expanded.coveredFraction)
    }

    func testPeekIsTheRestingDefaultCoverage() {
        XCTAssertEqual(SnapAnchor.peek.coveredFraction, 0.75, accuracy: 0.0001)
    }

    func testSmallDragStaysOnCurrentAnchor() {
        // A few points of jitter must not change the snap.
        XCTAssertEqual(resolve(from: .peek, drag: 8, predicted: 8), .peek)
        XCTAssertEqual(resolve(from: .peek, drag: -8, predicted: -8), .peek)
    }

    func testDragUpPastMidpointResolvesExpanded() {
        // From peek, dragging up far (negative) past halfway to expanded.
        let peekTop = SnapResolver.sheetTop(for: .peek, containerHeight: height)
        let expandedTop = SnapResolver.sheetTop(for: .expanded, containerHeight: height)
        let pastMidpoint = (expandedTop - peekTop) * 0.6
        XCTAssertEqual(resolve(from: .peek, drag: pastMidpoint, predicted: pastMidpoint), .expanded)
    }

    func testDragDownPastMidpointResolvesMapFocus() {
        let peekTop = SnapResolver.sheetTop(for: .peek, containerHeight: height)
        let mapFocusTop = SnapResolver.sheetTop(for: .mapFocus, containerHeight: height)
        let pastMidpoint = (mapFocusTop - peekTop) * 0.6
        XCTAssertEqual(resolve(from: .peek, drag: pastMidpoint, predicted: pastMidpoint), .mapFocus)
    }

    func testUpwardFlingFromPeekResolvesExpanded() {
        // Small physical drag, large predicted-end (high velocity) up.
        XCTAssertEqual(resolve(from: .peek, drag: -20, predicted: -600), .expanded)
    }

    func testDownwardFlingFromPeekResolvesMapFocus() {
        XCTAssertEqual(resolve(from: .peek, drag: 20, predicted: 600), .mapFocus)
    }

    func testUpwardFlingFromMapFocusReachesExpanded() {
        XCTAssertEqual(resolve(from: .mapFocus, drag: -40, predicted: -1200), .expanded)
    }

    func testClampAtTopDoesNotOvershootPastExpanded() {
        // Already expanded, fling further up — clamps at expanded, never beyond.
        XCTAssertEqual(resolve(from: .expanded, drag: -200, predicted: -2000), .expanded)
    }

    func testClampAtBottomDoesNotOvershootPastMapFocus() {
        // Already map-focus, fling further down — clamps at mapFocus, never hidden.
        XCTAssertEqual(resolve(from: .mapFocus, drag: 200, predicted: 2000), .mapFocus)
    }

    func testResolverNeverProducesHiddenAnchor() {
        for current in SnapAnchor.allCases {
            for predicted in stride(from: -2000.0 as CGFloat, through: 2000, by: 250) {
                let result = resolve(from: current, drag: predicted, predicted: predicted)
                XCTAssertTrue(SnapAnchor.allCases.contains(result))
            }
        }
    }

    func testZeroContainerHeightKeepsCurrentAnchor() {
        let result = SnapResolver.resolve(
            from: .expanded,
            dragTranslation: 100,
            predictedEndTranslation: 100,
            containerHeight: 0
        )
        XCTAssertEqual(result, .expanded)
    }

    func testSheetTopMatchesCoverageFraction() {
        // peek covers 0.75 → top sits at 0.25 of the container.
        XCTAssertEqual(SnapResolver.sheetTop(for: .peek, containerHeight: height), height * 0.25, accuracy: 0.001)
        XCTAssertEqual(SnapResolver.sheetTop(for: .mapFocus, containerHeight: height), height * 0.70, accuracy: 0.001)
        XCTAssertEqual(SnapResolver.sheetTop(for: .expanded, containerHeight: height), height * 0.05, accuracy: 0.001)
    }
}
