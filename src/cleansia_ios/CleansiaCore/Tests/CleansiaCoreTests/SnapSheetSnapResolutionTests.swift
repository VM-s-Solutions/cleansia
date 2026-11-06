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

    func testAShortViewportKeepsTheMeasuredFixedContentVisibleAtEveryAnchor() {
        for anchor in SnapAnchor.allCases {
            let top = SnapResolver.sheetTop(for: anchor, containerHeight: 600, minimumSheetHeight: 360)
            XCTAssertGreaterThanOrEqual(600 - top, 360, "\(anchor)")
        }
        XCTAssertEqual(
            SnapResolver.sheetTop(for: .mapFocus, containerHeight: 600, minimumSheetHeight: 360), 240
        )
    }

    func testATallViewportKeepsItsOriginalMapFocusWhenTheContentAlreadyFits() {
        XCTAssertEqual(
            SnapResolver.sheetTop(for: .mapFocus, containerHeight: 1000, minimumSheetHeight: 240), 700
        )
    }

    func testNegativeAndOversizedMinimumsStayInsideTheViewport() {
        for anchor in SnapAnchor.allCases {
            XCTAssertEqual(
                SnapResolver.sheetTop(for: anchor, containerHeight: 600, minimumSheetHeight: -100),
                SnapResolver.sheetTop(for: anchor, containerHeight: 600)
            )
            XCTAssertEqual(SnapResolver.sheetTop(for: anchor, containerHeight: 600, minimumSheetHeight: 900), 0)
            XCTAssertEqual(SnapResolver.sheetTop(for: anchor, containerHeight: 0, minimumSheetHeight: 900), 0)
        }
    }

    func testDraggingUsesTheFlooredAnchorPositions() {
        XCTAssertEqual(
            SnapResolver.resolve(
                from: .peek,
                dragTranslation: 60,
                predictedEndTranslation: 60,
                containerHeight: 600,
                minimumSheetHeight: 360
            ),
            .mapFocus
        )
    }

    func testDistinctAnchorMidpointTiesKeepTheirOriginalChoiceWithTheDefaultMinimum() {
        XCTAssertEqual(
            SnapResolver.resolve(
                from: .peek,
                dragTranslation: 225,
                predictedEndTranslation: 225,
                containerHeight: 1000
            ),
            .mapFocus
        )
        XCTAssertEqual(
            SnapResolver.resolve(
                from: .expanded,
                dragTranslation: 100,
                predictedEndTranslation: 100,
                containerHeight: 1000
            ),
            .peek
        )
    }

    func testCoincidentMapFocusAndPeekKeepTheirCurrentAnchorWithoutMovement() {
        for anchor in [SnapAnchor.mapFocus, .peek] {
            XCTAssertEqual(
                SnapResolver.resolve(
                    from: anchor,
                    dragTranslation: 0,
                    predictedEndTranslation: 0,
                    containerHeight: 400,
                    minimumSheetHeight: 350
                ),
                anchor
            )
        }
    }

    func testCoincidentAnchorPositionsPreserveTheCurrentAnchorWithoutMovement() {
        for anchor in SnapAnchor.allCases {
            XCTAssertEqual(
                SnapResolver.resolve(
                    from: anchor,
                    dragTranslation: 0,
                    predictedEndTranslation: 0,
                    containerHeight: 400,
                    minimumSheetHeight: 500
                ),
                anchor
            )
        }
    }

    // MARK: - A sheet with fewer stops and a ceiling

    private let twoStops: Set<SnapAnchor> = [.mapFocus, .expanded]

    func testTheExpandedTopStopsAtTheGivenTopAndTheOthersKeepTheirs() {
        XCTAssertEqual(
            SnapResolver.sheetTop(for: .expanded, containerHeight: height, minimumSheetTop: 136), 136
        )
        XCTAssertEqual(
            SnapResolver.sheetTop(for: .mapFocus, containerHeight: height, minimumSheetTop: 136), height * 0.70,
            accuracy: 0.001
        )
        XCTAssertEqual(
            SnapResolver.sheetTop(for: .expanded, containerHeight: height, minimumSheetTop: 0), height * 0.05,
            accuracy: 0.001
        )
    }

    func testASheetRestsOnlyOnItsStops() {
        let resolved = SnapResolver.resolve(
            from: .expanded,
            dragTranslation: 100,
            predictedEndTranslation: 100,
            containerHeight: height,
            stops: twoStops,
            minimumSheetTop: 136
        )
        XCTAssertEqual(resolved, .expanded, "the middle stop it does not have is never chosen")
        XCTAssertEqual(
            SnapResolver.resolve(
                from: .expanded,
                dragTranslation: 20,
                predictedEndTranslation: 600,
                containerHeight: height,
                stops: twoStops,
                minimumSheetTop: 136
            ),
            .mapFocus
        )
        XCTAssertEqual(
            SnapResolver.resolve(
                from: .mapFocus,
                dragTranslation: -40,
                predictedEndTranslation: -1200,
                containerHeight: height,
                stops: twoStops,
                minimumSheetTop: 136
            ),
            .expanded
        )
    }

    // MARK: - A sheet that collapses onto a measured height

    func testAGivenMapFocusHeightIsWhereTheCollapsedSheetRestsAndTheOtherStopsKeepTheirs() {
        XCTAssertEqual(SnapResolver.sheetTop(for: .mapFocus, containerHeight: 660, mapFocusHeight: 182), 478)
        XCTAssertEqual(SnapResolver.sheetTop(for: .mapFocus, containerHeight: 660, mapFocusHeight: 260), 400)
        XCTAssertEqual(
            SnapResolver.sheetTop(for: .expanded, containerHeight: 660, minimumSheetTop: 136, mapFocusHeight: 182), 136
        )
        XCTAssertEqual(
            SnapResolver.sheetTop(for: .peek, containerHeight: 660, mapFocusHeight: 182),
            SnapResolver.sheetTop(for: .peek, containerHeight: 660)
        )
        XCTAssertEqual(
            SnapResolver.sheetTop(for: .mapFocus, containerHeight: 660, minimumSheetHeight: 240, mapFocusHeight: 182),
            420,
            "the floor still holds"
        )
        XCTAssertEqual(SnapResolver.sheetTop(for: .mapFocus, containerHeight: 660, mapFocusHeight: 900), 0)
    }

    func testWithNoMapFocusHeightEveryStopIsWhereItWas() {
        for anchor in SnapAnchor.allCases {
            for minimum: CGFloat in [0, 240] {
                XCTAssertEqual(
                    SnapResolver.sheetTop(
                        for: anchor,
                        containerHeight: 661.5,
                        minimumSheetHeight: minimum,
                        minimumSheetTop: 136,
                        mapFocusHeight: nil
                    ),
                    min(max(661.5 * (1 - anchor.coveredFraction), 136), 661.5 - minimum),
                    "\(anchor)"
                )
            }
        }
    }

    /// Measured against the given height's stop, 560 here: a drag short of halfway to it, which would pass halfway to
    /// the container's share at 462, stays expanded.
    func testDraggingDownSettlesOnTheMapFocusHeight() {
        let top = { (anchor: SnapAnchor) in
            SnapResolver.sheetTop(
                for: anchor,
                containerHeight: 660,
                minimumSheetTop: 136,
                mapFocusHeight: 100
            )
        }
        let past = (top(.mapFocus) - top(.expanded)) * 0.6
        XCTAssertEqual(
            SnapResolver.resolve(
                from: .expanded,
                dragTranslation: past,
                predictedEndTranslation: past,
                containerHeight: 660,
                stops: twoStops,
                minimumSheetTop: 136,
                mapFocusHeight: 100
            ),
            .mapFocus
        )
        let short = (top(.mapFocus) - top(.expanded)) * 0.45
        XCTAssertEqual(
            SnapResolver.resolve(
                from: .expanded,
                dragTranslation: short,
                predictedEndTranslation: short,
                containerHeight: 660,
                stops: twoStops,
                minimumSheetTop: 136,
                mapFocusHeight: 100
            ),
            .expanded
        )
    }

    func testAStepToAMissingStopGoesOnToTheNextOneThatWay() {
        XCTAssertEqual(SnapAnchor.mapFocus.toward(.peek, within: twoStops), .expanded)
        XCTAssertEqual(SnapAnchor.expanded.toward(.peek, within: twoStops), .mapFocus)
        XCTAssertEqual(SnapAnchor.expanded.toward(.mapFocus, within: twoStops), .mapFocus)
        XCTAssertEqual(SnapAnchor.expanded.toward(.expanded, within: twoStops), .expanded)
        XCTAssertEqual(SnapAnchor.mapFocus.toward(.peek, within: Set(SnapAnchor.allCases)), .peek)
        XCTAssertEqual(SnapAnchor.mapFocus.toward(.peek, within: [.mapFocus]), .mapFocus)
    }
}
