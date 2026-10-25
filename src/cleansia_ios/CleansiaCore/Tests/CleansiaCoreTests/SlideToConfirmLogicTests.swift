import XCTest
@testable import CleansiaCore

final class SlideToConfirmLogicTests: XCTestCase {
    func testDragClampsToTrackBounds() {
        var thumb = SlideToConfirmThumb()

        thumb.drag(translation: -40, maxX: 200)
        XCTAssertEqual(thumb.offset, 0)

        thumb.drag(translation: 120, maxX: 200)
        XCTAssertEqual(thumb.offset, 120)

        thumb.drag(translation: 500, maxX: 200)
        XCTAssertEqual(thumb.offset, 200)
    }

    func testEndDragAtThresholdFiresAndLocksAtEnd() {
        var thumb = SlideToConfirmThumb()
        thumb.drag(translation: 180, maxX: 200)

        XCTAssertTrue(thumb.endDrag(maxX: 200))
        XCTAssertEqual(thumb.offset, 200)
        XCTAssertTrue(thumb.hasFired)
    }

    func testEndDragJustBelowThresholdSpringsBack() {
        var thumb = SlideToConfirmThumb()
        thumb.drag(translation: 179, maxX: 200)

        XCTAssertFalse(thumb.endDrag(maxX: 200))
        XCTAssertEqual(thumb.offset, 0)
        XCTAssertFalse(thumb.hasFired)
    }

    func testDragAndEndDragAreIgnoredWhileFired() {
        var thumb = SlideToConfirmThumb()
        thumb.drag(translation: 200, maxX: 200)
        XCTAssertTrue(thumb.endDrag(maxX: 200))

        thumb.drag(translation: 10, maxX: 200)
        XCTAssertEqual(thumb.offset, 200)

        XCTAssertFalse(thumb.endDrag(maxX: 200))
    }

    func testResetSnapsBackAndAllowsRefire() {
        var thumb = SlideToConfirmThumb()
        thumb.drag(translation: 200, maxX: 200)
        XCTAssertTrue(thumb.endDrag(maxX: 200))

        thumb.reset()
        XCTAssertEqual(thumb.offset, 0)
        XCTAssertFalse(thumb.hasFired)

        thumb.drag(translation: 195, maxX: 200)
        XCTAssertTrue(thumb.endDrag(maxX: 200))
    }

    func testZeroWidthTrackNeverFires() {
        var thumb = SlideToConfirmThumb()
        thumb.drag(translation: 50, maxX: 0)
        XCTAssertEqual(thumb.offset, 0)
        XCTAssertFalse(thumb.endDrag(maxX: 0))
    }

    func testProgressReflectsOffsetFraction() {
        var thumb = SlideToConfirmThumb()
        thumb.drag(translation: 50, maxX: 200)
        XCTAssertEqual(thumb.progress(maxX: 200), 0.25, accuracy: 0.001)
        XCTAssertEqual(thumb.progress(maxX: 0), 0)
    }

    /// A fold resizes the app mid-action, so a thumb parked at the end of a track that then shrinks reads as at
    /// the end of the shorter one rather than past it.
    func testAThumbParkedAtTheEndOfATrackThatShrankReadsAsAtItsEnd() {
        var thumb = SlideToConfirmThumb()
        thumb.drag(translation: 500, maxX: 500)
        XCTAssertTrue(thumb.endDrag(maxX: 500))

        XCTAssertEqual(thumb.progress(maxX: 300), 1)
        XCTAssertEqual(thumb.progress(maxX: 500), 1)
    }

    func testTheThumbIsDrawnOnTheTrackWhateverWidthTheTrackHasNow() {
        var parked = SlideToConfirmThumb()
        parked.drag(translation: 500, maxX: 500)
        XCTAssertTrue(parked.endDrag(maxX: 500))
        XCTAssertEqual(parked.drawnOffset(maxX: 300), 300, "a fold shrank the track under the parked thumb")
        XCTAssertEqual(parked.drawnOffset(maxX: 500), 500)
        XCTAssertEqual(parked.drawnOffset(maxX: 0), 0)

        var midway = SlideToConfirmThumb()
        midway.drag(translation: 120, maxX: 200)
        XCTAssertEqual(midway.drawnOffset(maxX: 100), 100)
        XCTAssertEqual(midway.drawnOffset(maxX: 300), 120, "a wider track leaves the thumb under the finger")
    }

    /// The pure clamps hold only if the control draws from them.
    func testTheSlideDrawsItsThumbAndLabelFromTheClampedValues() throws {
        let source = try String(
            contentsOf: URL(fileURLWithPath: #filePath)
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .appendingPathComponent("Sources/CleansiaCore/Components/SlideToConfirm.swift"),
            encoding: .utf8
        )
        .components(separatedBy: .whitespacesAndNewlines)
        .joined()

        XCTAssertTrue(source.contains(".offset(x:thumb.drawnOffset(maxX:maxX))"))
        XCTAssertTrue(source.contains(".opacity(isBusy?1:1-Double(thumb.progress(maxX:maxX)))"))
        XCTAssertFalse(source.contains(".offset(x:thumb.offset)"))
    }
}
