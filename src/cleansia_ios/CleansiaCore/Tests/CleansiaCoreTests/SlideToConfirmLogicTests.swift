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
}
