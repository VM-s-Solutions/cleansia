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
        XCTAssertEqual(AnimatedMascotPlayback.nextChunkLength(published: 16, remaining: 47, firstChunk: 4), 16)
    }

    func testChunkLengthIsCappedByTheFramesLeft() {
        XCTAssertEqual(AnimatedMascotPlayback.nextChunkLength(published: 32, remaining: 20, firstChunk: 4), 20)
        XCTAssertEqual(AnimatedMascotPlayback.nextChunkLength(published: 0, remaining: 2, firstChunk: 4), 2)
    }

    func testChunkLengthIsZeroWhenNothingIsLeft() {
        XCTAssertEqual(AnimatedMascotPlayback.nextChunkLength(published: 32, remaining: 0, firstChunk: 4), 0)
    }

    // MARK: shouldPublish

    func testPublishesTheFirstChunkAsSoonAsItIsDecoded() {
        XCTAssertTrue(AnimatedMascotPlayback.shouldPublish(decoded: 4, published: 0, isComplete: false, firstChunk: 4))
    }

    func testDoesNotPublishBeforeTheFirstChunkIsWhole() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldPublish(decoded: 3, published: 0, isComplete: false, firstChunk: 4))
    }

    func testPublishesOnDoubling() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldPublish(decoded: 7, published: 4, isComplete: false, firstChunk: 4))
        XCTAssertTrue(AnimatedMascotPlayback.shouldPublish(decoded: 8, published: 4, isComplete: false, firstChunk: 4))
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

    // MARK: preferredFramesPerSecond

    func testDisplayLinkOversamplesTheAnimationRate() {
        XCTAssertEqual(AnimatedMascotPlayback.preferredFramesPerSecond(shortestDelay: 0.041), 49)
        XCTAssertEqual(AnimatedMascotPlayback.preferredFramesPerSecond(shortestDelay: 0.06), 34)
    }

    func testDisplayLinkRateIsCappedAtTheDisplayRate() {
        XCTAssertEqual(AnimatedMascotPlayback.preferredFramesPerSecond(shortestDelay: 1.0 / 30.0), 60)
        XCTAssertEqual(AnimatedMascotPlayback.preferredFramesPerSecond(shortestDelay: 1.0 / 120.0), 60)
    }

    func testDisplayLinkRateHasAFloorForSlowAnimations() {
        XCTAssertEqual(AnimatedMascotPlayback.preferredFramesPerSecond(shortestDelay: 0.5), 30)
    }

    func testDisplayLinkRateSurvivesAMissingDelay() {
        XCTAssertEqual(AnimatedMascotPlayback.preferredFramesPerSecond(shortestDelay: 0), 60)
        XCTAssertEqual(AnimatedMascotPlayback.preferredFramesPerSecond(shortestDelay: 1e-300), 60)
    }

    // MARK: asset names

    func testAnimatedMascotAssetNames() {
        XCTAssertEqual(AnimatedMascot.cleaningInProgress.rawValue, "mascot_cleaning_in_progress")
        XCTAssertEqual(AnimatedMascot.welcoming.rawValue, "mascot_welcoming")
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
