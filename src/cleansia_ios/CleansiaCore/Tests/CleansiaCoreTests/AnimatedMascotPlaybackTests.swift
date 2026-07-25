import XCTest
@testable import CleansiaCore

final class AnimatedMascotPlaybackTests: XCTestCase {
    // MARK: shouldRestart

    func testFirstRenderRestarts() {
        XCTAssertTrue(AnimatedMascotPlayback.shouldRestart(
            currentName: nil, currentLoop: nil, name: "m", loop: true, force: false
        ))
    }

    func testSameMascotAndLoopDoesNotRestart() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldRestart(
            currentName: "m", currentLoop: true, name: "m", loop: true, force: false
        ))
    }

    func testForceAlwaysRestarts() {
        XCTAssertTrue(AnimatedMascotPlayback.shouldRestart(
            currentName: "m", currentLoop: true, name: "m", loop: true, force: true
        ))
    }

    func testChangedMascotRestarts() {
        XCTAssertTrue(AnimatedMascotPlayback.shouldRestart(
            currentName: "m", currentLoop: true, name: "other", loop: true, force: false
        ))
    }

    func testChangedLoopRestarts() {
        XCTAssertTrue(AnimatedMascotPlayback.shouldRestart(
            currentName: "m", currentLoop: false, name: "m", loop: true, force: false
        ))
    }

    // MARK: isSuperseded

    func testCurrentGenerationIsNotSuperseded() {
        XCTAssertFalse(AnimatedMascotPlayback.isSuperseded(token: 2, generation: 2))
    }

    func testOlderGenerationIsSuperseded() {
        XCTAssertTrue(AnimatedMascotPlayback.isSuperseded(token: 1, generation: 2))
    }

    // MARK: shouldResumePlayback

    func testResumesWhenOnWindowWithFramesAndStopped() {
        XCTAssertTrue(AnimatedMascotPlayback.shouldResumePlayback(
            hasWindow: true,
            hasFrames: true,
            isAnimating: false,
            isFinished: false
        ))
    }

    func testDoesNotResumeOffWindow() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldResumePlayback(
            hasWindow: false,
            hasFrames: true,
            isAnimating: false,
            isFinished: false
        ))
    }

    func testDoesNotResumeWithoutFrames() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldResumePlayback(
            hasWindow: true,
            hasFrames: false,
            isAnimating: false,
            isFinished: false
        ))
    }

    func testDoesNotResumeWhileAlreadyAnimating() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldResumePlayback(
            hasWindow: true,
            hasFrames: true,
            isAnimating: true,
            isFinished: false
        ))
    }

    /// A one-shot mascot that already settled on its final frame must NOT restart its display link when
    /// the view re-enters a window (scroll away and back, tab switch, sheet dismissal) — it would then
    /// tick every display frame forever with nothing to draw.
    func testDoesNotResumeAFinishedOneShot() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldResumePlayback(
            hasWindow: true,
            hasFrames: true,
            isAnimating: false,
            isFinished: true
        ))
    }

    // MARK: normalizedDelay

    func testNormalizedDelayKeepsAPositiveDelay() {
        XCTAssertEqual(AnimatedMascotPlayback.normalizedDelay(0.041), 0.041, accuracy: 0.0001)
    }

    func testNormalizedDelayReplacesZeroWith30fps() {
        XCTAssertEqual(AnimatedMascotPlayback.normalizedDelay(0), 1.0 / 30.0, accuracy: 0.0001)
    }

    func testNormalizedDelayReplacesNegativeWith30fps() {
        XCTAssertEqual(AnimatedMascotPlayback.normalizedDelay(-1), 1.0 / 30.0, accuracy: 0.0001)
    }

    func testNormalizedDelayReplacesNonFiniteWith30fps() {
        XCTAssertEqual(AnimatedMascotPlayback.normalizedDelay(.nan), 1.0 / 30.0, accuracy: 0.0001)
        XCTAssertEqual(AnimatedMascotPlayback.normalizedDelay(.infinity), 1.0 / 30.0, accuracy: 0.0001)
    }

    // MARK: advance — stepping

    func testAdvanceHoldsTheFrameUntilItsDelayElapsed() {
        let head = AnimatedMascotPlayback.Playhead(index: 0, frameStart: 0, isFinished: false)
        XCTAssertEqual(step(head, now: 0.02), head)
    }

    func testAdvanceStepsOneFrameWhenTheDelayElapsed() {
        let next = step(AnimatedMascotPlayback.Playhead(index: 0, frameStart: 0, isFinished: false), now: 0.05)
        XCTAssertEqual(next.index, 1)
        XCTAssertEqual(next.frameStart, 0.04, accuracy: 0.0001)
        XCTAssertFalse(next.isFinished)
    }

    func testAdvanceStepsOnlyOneFrameEvenWhenTwoAreDue() {
        let next = step(AnimatedMascotPlayback.Playhead(index: 0, frameStart: 0, isFinished: false), now: 0.09)
        XCTAssertEqual(next.index, 1)
        XCTAssertEqual(next.frameStart, 0.04, accuracy: 0.0001, "a small backlog is caught up one tick at a time")
    }

    func testAdvanceIgnoresAnEmptyBuffer() {
        let head = AnimatedMascotPlayback.Playhead(index: 0, frameStart: 0, isFinished: false)
        XCTAssertEqual(
            AnimatedMascotPlayback.advance(head, now: 10, delays: [], isComplete: false, loop: true),
            head
        )
    }

    func testAdvanceClampsAnIndexBeyondTheBuffer() {
        let next = step(AnimatedMascotPlayback.Playhead(index: 9, frameStart: 0, isFinished: false), now: 1)
        XCTAssertEqual(next.index, 2)
        XCTAssertEqual(next.frameStart, 1, accuracy: 0.0001)
    }

    func testAdvanceTreatsAZeroDelayAs30fps() {
        let head = AnimatedMascotPlayback.Playhead(index: 0, frameStart: 0, isFinished: false)
        let zeroDelays = [0.0, 0.0, 0.0]
        XCTAssertEqual(
            AnimatedMascotPlayback.advance(head, now: 0.02, delays: zeroDelays, isComplete: true, loop: true),
            head
        )
        let next = AnimatedMascotPlayback.advance(head, now: 0.04, delays: zeroDelays, isComplete: true, loop: true)
        XCTAssertEqual(next.index, 1)
    }

    // MARK: advance — partial buffer

    func testAdvanceHoldsTheLastDecodedFrameWhileTheDecoderIsBehind() {
        let head = AnimatedMascotPlayback.Playhead(index: 2, frameStart: 0, isFinished: false)
        let next = step(head, now: 0.05, isComplete: false)
        XCTAssertEqual(next.index, 2, "no frame 3 exists yet — hold instead of wrapping or finishing")
        XCTAssertFalse(next.isFinished)
    }

    func testHoldingRebasesTheFrameClockSoTheNextDecodedFrameGetsItsFullDelay() {
        let head = AnimatedMascotPlayback.Playhead(index: 2, frameStart: 0, isFinished: false)
        let held = step(head, now: 5, isComplete: false)
        XCTAssertEqual(held.frameStart, 5, accuracy: 0.0001)
        XCTAssertEqual(step(held, now: 5.01, isComplete: false).index, 2)
        let grown = AnimatedMascotPlayback.advance(
            held,
            now: 5.05,
            delays: [0.04, 0.04, 0.04, 0.04],
            isComplete: false,
            loop: true
        )
        XCTAssertEqual(grown.index, 3, "the frame published while holding plays after a full delay")
    }

    func testAdvanceDoesNotWrapAPartialBufferEvenWhenLooping() {
        let head = AnimatedMascotPlayback.Playhead(index: 2, frameStart: 0, isFinished: false)
        XCTAssertEqual(step(head, now: 1, isComplete: false).index, 2)
    }

    // MARK: advance — loop and one-shot ends

    func testAdvanceWrapsAtTheEndOfACompleteLoop() {
        let next = step(AnimatedMascotPlayback.Playhead(index: 2, frameStart: 0, isFinished: false), now: 0.05)
        XCTAssertEqual(next.index, 0)
        XCTAssertFalse(next.isFinished)
        XCTAssertEqual(next.frameStart, 0.04, accuracy: 0.0001)
    }

    func testAdvanceFinishesAtTheEndOfAOneShot() {
        let head = AnimatedMascotPlayback.Playhead(index: 2, frameStart: 0, isFinished: false)
        let next = step(head, now: 0.05, loop: false)
        XCTAssertTrue(next.isFinished)
        XCTAssertEqual(next.index, 2, "a one-shot freezes on its final frame")
    }

    func testAFinishedPlayheadNeverAdvancesAgain() {
        let head = AnimatedMascotPlayback.Playhead(index: 2, frameStart: 0, isFinished: true)
        XCTAssertEqual(step(head, now: 99, loop: false), head)
        XCTAssertEqual(step(head, now: 99), head)
    }

    // MARK: advance — overdue rebase

    func testAdvanceRebasesInsteadOfCatchingUpWhenManyFramesAreOverdue() {
        let next = step(AnimatedMascotPlayback.Playhead(index: 0, frameStart: 0, isFinished: false), now: 10)
        XCTAssertEqual(next.index, 1, "still exactly one step")
        XCTAssertEqual(next.frameStart, 10, accuracy: 0.0001, "the 250-frame backlog is dropped, not replayed")
    }

    func testAdvanceRebasesTheWrapAfterALongStall() {
        let next = step(AnimatedMascotPlayback.Playhead(index: 2, frameStart: 0, isFinished: false), now: 10)
        XCTAssertEqual(next.index, 0)
        XCTAssertEqual(next.frameStart, 10, accuracy: 0.0001)
    }

    // MARK: firstChunkSize

    func testFirstChunkIsSmallEnoughToStartAlmostImmediately() {
        XCTAssertEqual(AnimatedMascotPlayback.firstChunkSize(frameCount: 63), 4)
        XCTAssertEqual(AnimatedMascotPlayback.firstChunkSize(frameCount: 125), 4)
    }

    func testFirstChunkNeverExceedsTheFrameCount() {
        XCTAssertEqual(AnimatedMascotPlayback.firstChunkSize(frameCount: 2), 2)
        XCTAssertEqual(AnimatedMascotPlayback.firstChunkSize(frameCount: 0), 0)
    }

    // MARK: nextChunkLength

    func testFirstChunkLengthUsesTheFirstChunkSize() {
        XCTAssertEqual(AnimatedMascotPlayback.nextChunkLength(published: 0, remaining: 63, firstChunk: 4), 4)
    }

    func testChunkLengthDoublesTheBuffer() {
        XCTAssertEqual(AnimatedMascotPlayback.nextChunkLength(published: 4, remaining: 59, firstChunk: 4), 4)
        XCTAssertEqual(AnimatedMascotPlayback.nextChunkLength(published: 8, remaining: 55, firstChunk: 4), 8)
    }

    /// Unbounded doubling made the LAST chunk of a 125-frame loop cover ~61 frames, and nothing is
    /// published until a chunk is whole — the playhead then holds one frame for seconds.
    func testChunkLengthStopsDoublingAtTheCap() {
        XCTAssertEqual(AnimatedMascotPlayback.nextChunkLength(published: 16, remaining: 47, firstChunk: 4), 8)
        XCTAssertEqual(AnimatedMascotPlayback.nextChunkLength(published: 64, remaining: 61, firstChunk: 4), 8)
    }

    func testChunkLengthIsCappedByTheFramesLeft() {
        XCTAssertEqual(AnimatedMascotPlayback.nextChunkLength(published: 32, remaining: 3, firstChunk: 4), 3)
        XCTAssertEqual(AnimatedMascotPlayback.nextChunkLength(published: 0, remaining: 2, firstChunk: 4), 2)
    }

    func testChunkLengthIsZeroWhenNothingIsLeft() {
        XCTAssertEqual(AnimatedMascotPlayback.nextChunkLength(published: 32, remaining: 0, firstChunk: 4), 0)
    }

    // MARK: nextChunkSlice

    func testTheFirstSliceIsTheFirstChunkOfTheFirstSegment() {
        XCTAssertEqual(
            AnimatedMascotPlayback.nextChunkSlice(published: 0, cursor: 0, counts: [18, 18, 17], firstChunk: 4),
            AnimatedMascotPlayback.ChunkSlice(segment: 0, range: 0 ..< 4)
        )
    }

    func testASliceNeverStraddlesTwoSegments() {
        XCTAssertEqual(
            AnimatedMascotPlayback.nextChunkSlice(published: 16, cursor: 16, counts: [18, 18, 17], firstChunk: 4),
            AnimatedMascotPlayback.ChunkSlice(segment: 0, range: 16 ..< 18),
            "the chunk is clipped at the segment end instead of spilling into the next file"
        )
    }

    func testASliceRestartsAtLocalZeroInTheNextSegment() {
        XCTAssertEqual(
            AnimatedMascotPlayback.nextChunkSlice(published: 16, cursor: 18, counts: [18, 18, 17], firstChunk: 4),
            AnimatedMascotPlayback.ChunkSlice(segment: 1, range: 0 ..< 8),
            "indices are local to the segment's own CGImageSource"
        )
    }

    func testASliceLandsInTheLastSegment() {
        XCTAssertEqual(
            AnimatedMascotPlayback.nextChunkSlice(
                published: 32, cursor: 120, counts: [18, 18, 18, 18, 18, 18, 17], firstChunk: 4
            ),
            AnimatedMascotPlayback.ChunkSlice(segment: 6, range: 12 ..< 17),
            "the tail chunk is clipped by the frames that are left, not by the cap"
        )
    }

    func testASingleSegmentBehavesLikeAWholeFile() {
        XCTAssertEqual(
            AnimatedMascotPlayback.nextChunkSlice(published: 0, cursor: 0, counts: [50], firstChunk: 4),
            AnimatedMascotPlayback.ChunkSlice(segment: 0, range: 0 ..< 4)
        )
        XCTAssertEqual(
            AnimatedMascotPlayback.nextChunkSlice(published: 8, cursor: 8, counts: [50], firstChunk: 4),
            AnimatedMascotPlayback.ChunkSlice(segment: 0, range: 8 ..< 16)
        )
    }

    func testThereIsNoSliceOnceEveryFrameIsDecoded() {
        XCTAssertNil(
            AnimatedMascotPlayback.nextChunkSlice(published: 53, cursor: 53, counts: [18, 18, 17], firstChunk: 4)
        )
        XCTAssertNil(AnimatedMascotPlayback.nextChunkSlice(published: 0, cursor: 0, counts: [], firstChunk: 4))
    }

    /// Walking the whole plan must visit every frame of every segment exactly once, in order — that
    /// concatenation IS the animation the playhead sees.
    func testWalkingEverySliceCoversTheSegmentsInOrder() {
        let counts = [18, 18, 18, 18, 18, 18, 17]
        var cursor = 0
        var visited: [Int: [Int]] = [:]
        var chunks = 0
        while let slice = AnimatedMascotPlayback.nextChunkSlice(
            published: cursor, cursor: cursor, counts: counts, firstChunk: 4
        ) {
            visited[slice.segment, default: []] += Array(slice.range)
            cursor += slice.range.count
            chunks += 1
            XCTAssertLessThan(chunks, 200, "slice walk must terminate")
        }
        XCTAssertEqual(cursor, 125)
        for (segment, frames) in visited {
            XCTAssertEqual(frames, Array(0 ..< counts[segment]), "segment \(segment) decoded out of order")
        }
    }

    // MARK: shouldPublish

    func testPublishesTheFirstChunkAsSoonAsItIsDecoded() {
        XCTAssertTrue(AnimatedMascotPlayback.shouldPublish(decoded: 4, published: 0, isComplete: false, firstChunk: 4))
    }

    func testDoesNotPublishBeforeTheFirstChunkIsWhole() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldPublish(decoded: 3, published: 0, isComplete: false, firstChunk: 4))
    }

    /// Publishing must NOT wait for the buffer to double: at `published: 64` that meant 61 more frames
    /// decoded before anything reached the screen — the visible drag. Every chunk goes out.
    func testPublishesEveryChunkOncePlaybackHasStarted() {
        XCTAssertTrue(AnimatedMascotPlayback.shouldPublish(decoded: 5, published: 4, isComplete: false, firstChunk: 4))
        XCTAssertTrue(AnimatedMascotPlayback.shouldPublish(
            decoded: 18, published: 16, isComplete: false, firstChunk: 4
        ))
        XCTAssertTrue(AnimatedMascotPlayback.shouldPublish(
            decoded: 72, published: 64, isComplete: false, firstChunk: 4
        ))
    }

    func testAlwaysPublishesTheCompleteAnimation() {
        XCTAssertTrue(AnimatedMascotPlayback.shouldPublish(decoded: 33, published: 32, isComplete: true, firstChunk: 4))
        XCTAssertTrue(AnimatedMascotPlayback.shouldPublish(decoded: 1, published: 0, isComplete: true, firstChunk: 4))
    }

    func testDoesNotPublishWithoutNewFrames() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldPublish(
            decoded: 32,
            published: 32,
            isComplete: true,
            firstChunk: 4
        ))
    }

    // MARK: display cadence at the native rate

    /// The judder: the link used to be pinned to the animation's own rate, which CADisplayLink quantizes
    /// to a factor of the panel rate — a 24 fps loop asked for 49 and got 30 Hz, holding alternate frames
    /// for 67 ms against 33 ms. Ticking at the panel's own 60 Hz and letting `advance` gate on the
    /// timestamp holds every frame for 2 or 3 ticks instead.
    func testEveryFrameHoldsTwoOrThreeTicksAtTheNativeDisplayRate() {
        let holds = displayedFrameDurations(tick: 1.0 / 60.0, delays: mascotDelays)
        let ticks = holds.map { Int(($0 * 60).rounded()) }
        XCTAssertEqual(Set(ticks), [2, 3], "no frame is held for one tick or for four")
    }

    /// A frame clock that drifts would slowly desync the 24 fps loop from its 5.207 s length.
    func testTheLoopKeepsItsLengthAtTheNativeDisplayRate() {
        let delays = mascotDelays
        let holds = displayedFrameDurations(tick: 1.0 / 60.0, delays: delays)
        XCTAssertEqual(holds.reduce(0, +) + delays[delays.count - 1], 5.207, accuracy: 1.0 / 60.0)
    }

    // MARK: segments

    func testAnimatedMascotAssetNames() {
        XCTAssertEqual(AnimatedMascot.cleaningInProgress.rawValue, "mascot_cleaning_in_progress")
        XCTAssertEqual(AnimatedMascot.welcoming.rawValue, "mascot_welcoming")
    }

    func testTheCleaningLoopIsSplitAcrossSevenSegmentsInOrder() {
        XCTAssertEqual(AnimatedMascot.cleaningInProgress.segmentNames, [
            "mascot_cleaning_in_progress",
            "mascot_cleaning_in_progress_1",
            "mascot_cleaning_in_progress_2",
            "mascot_cleaning_in_progress_3",
            "mascot_cleaning_in_progress_4",
            "mascot_cleaning_in_progress_5",
            "mascot_cleaning_in_progress_6"
        ])
    }

    func testAShortMascotStaysASingleFile() {
        XCTAssertEqual(AnimatedMascot.welcoming.segmentNames, ["mascot_welcoming"])
    }

    /// Both mascots decode at the asset's NATIVE 360 px. Decode size is a pure memory lever (measured:
    /// 360/288/240/180 all decode the 125-frame loop in ~4.2 s), so shrinking it buys nothing but a softer
    /// image: the heroes render in a 140 pt box = 420 px at @3x, where 360 already upscales 1.17x and 240
    /// would upscale 1.75x on the app's most prominent animation.
    func testDecodeSizes() {
        XCTAssertEqual(AnimatedMascot.cleaningInProgress.maxPixel, 360)
        XCTAssertEqual(AnimatedMascot.welcoming.maxPixel, 360)
    }

    func testMascotAssetNames() {
        XCTAssertEqual(Mascot.waving.rawValue, "mascot_waving")
        XCTAssertEqual(Mascot.leaning.rawValue, "mascot_leaning")
        XCTAssertEqual(Mascot.cleaning.rawValue, "mascot_cleaning")
        XCTAssertEqual(Mascot.ready.rawValue, "mascot_ready")
        XCTAssertEqual(Mascot.idea.rawValue, "mascot_idea")
        XCTAssertEqual(Mascot.mopping.rawValue, "mascot_mopping")
    }

    // MARK: helpers

    /// The shipped 125-frame cleaning loop: 24 fps written in whole milliseconds, 5207 ms round.
    private var mascotDelays: [TimeInterval] {
        Array(repeating: [0.041, 0.042, 0.042], count: 41).flatMap { $0 } + [0.041, 0.041]
    }

    /// How long each frame actually stays on screen when `advance` is driven by a display link ticking
    /// at `tick`. One entry per frame CHANGE, so the final (still-showing) frame is not included.
    private func displayedFrameDurations(tick: TimeInterval, delays: [TimeInterval]) -> [TimeInterval] {
        var playhead = AnimatedMascotPlayback.Playhead()
        var durations: [TimeInterval] = []
        var shownAt: TimeInterval = 0
        var now: TimeInterval = 0
        while durations.count < delays.count - 1, now < 60 {
            now += tick
            let next = AnimatedMascotPlayback.advance(
                playhead, now: now, delays: delays, isComplete: true, loop: false
            )
            if next.index != playhead.index {
                durations.append(now - shownAt)
                shownAt = now
            }
            playhead = next
        }
        XCTAssertEqual(durations.count, delays.count - 1, "the loop must run to its last frame")
        return durations
    }

    /// Three 40 ms frames — the shipped mascots run at 41 ms.
    private func step(
        _ playhead: AnimatedMascotPlayback.Playhead,
        now: TimeInterval,
        isComplete: Bool = true,
        loop: Bool = true
    ) -> AnimatedMascotPlayback.Playhead {
        AnimatedMascotPlayback.advance(
            playhead,
            now: now,
            delays: [0.04, 0.04, 0.04],
            isComplete: isComplete,
            loop: loop
        )
    }
}
