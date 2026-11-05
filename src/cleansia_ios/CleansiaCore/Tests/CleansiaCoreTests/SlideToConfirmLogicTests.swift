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

    func testALockedSlideIsSpokenAsItsReasonThenItsCountdown() {
        XCTAssertEqual(
            SlideToConfirm.lockedLabel(reason: "You can set off from 13:00", detail: "in 3 h 40 min"),
            "You can set off from 13:00, in 3 h 40 min"
        )
        XCTAssertEqual(SlideToConfirm.lockedLabel(reason: "Not yet", detail: nil), "Not yet")
        XCTAssertEqual(SlideToConfirm.lockedLabel(reason: "Not yet", detail: " "), "Not yet")
    }

    /// The locked slide has no thumb to drag and reads as a disabled button; every open slide keeps its defaults.
    func testALockedSlideNeverDragsAndIsADisabledButton() throws {
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
        let locked = try XCTUnwrap(source.range(of: "privatefunclockedTrack(").map { String(source[$0.lowerBound...]) })
            .components(separatedBy: "private").dropFirst().first ?? ""

        XCTAssertFalse(locked.isEmpty)
        XCTAssertFalse(locked.contains("gesture("), "a locked slide has nothing to drag")
        XCTAssertTrue(locked.contains(".accessibilityAddTraits(.isButton).disabled(true)"))
        XCTAssertTrue(locked.contains("Image(systemName:\"lock\")"))
        XCTAssertTrue(source.contains(
            "enabled:Bool=true,resetTrigger:Int=0,maxLabelLines:Int=1,thumbLanes:Bool=false,"
        ))
    }

    /// The locked slide is the prominent track in its locked face: the words in the type tokens, centred between
    /// the lanes, a ringed lock where the thumb would be, and no dimming, because it is a state, not a disabled
    /// control.
    func testTheLockedSlideIsTheProminentTrackInItsLockedFace() throws {
        let source = try compactSlideSource()
        let locked = try XCTUnwrap(source.range(of: "privatefunclockedTrack(").map { String(source[$0.lowerBound...]) })
            .components(separatedBy: "private").dropFirst().first ?? ""

        XCTAssertTrue(locked.contains("lanedShell(fill:CleansiaColors.surfaceVariant,lanes:true)"))
        XCTAssertTrue(locked.contains(".font(CleansiaTypography.labelLarge).foregroundColor(CleansiaColors.onSurface)"))
        XCTAssertTrue(locked.contains(
            ".font(CleansiaTypography.labelMedium).foregroundColor(CleansiaColors.onSurfaceVariant)"
        ))
        XCTAssertTrue(locked.contains(".strokeBorder(CleansiaColors.outline,lineWidth:1.5)"))
        XCTAssertFalse(locked.contains(".nunito("), "no hand-set face")
        XCTAssertFalse(locked.contains("words("))
        XCTAssertFalse(locked.contains(".opacity("), "a locked slide is not dimmed")
    }

    /// Whichever track an open slide draws, it springs back when its busy state ends and when the caller bumps
    /// `resetTrigger`, so a failed submit hands the thumb back.
    func testEveryOpenSlideSpringsBackWhenItsBusyStateEndsOrItsResetTriggerMoves() throws {
        let source = try compactSlideSource()

        XCTAssertTrue(source.contains(
            "openTrack.onChange(of:isBusy){busyinif!busy{withAnimation(.spring()){thumb.reset()}}}"
                + ".onChange(of:resetTrigger){_inwithAnimation(.spring()){thumb.reset()}}"
        ))
        XCTAssertEqual(source.components(separatedBy: ".onChange(of:isBusy)").count, 2, "one place for every track")
    }

    /// An opt-in reaches the laned track; with none the customer's fixed track is drawn as before.
    func testOnlyAnOptInDrawsTheLanedTrack() throws {
        let source = try compactSlideSource()

        XCTAssertTrue(source.contains("ifthumbLanes||maxLabelLines>1{lanedTrack}else{slidingTrack}"))
    }

    private func compactSlideSource() throws -> String {
        try String(
            contentsOf: URL(fileURLWithPath: #filePath)
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .deletingLastPathComponent()
                .appendingPathComponent("Sources/CleansiaCore/Components/SlideToConfirm.swift"),
            encoding: .utf8
        )
        .components(separatedBy: .whitespacesAndNewlines)
        .joined()
    }
}
