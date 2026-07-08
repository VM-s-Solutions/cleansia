import XCTest
@testable import CleansiaCore

final class AnimatedMascotPlaybackTests: XCTestCase {
    func testLoopingNeverStops() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldStop(loop: true, frameIndex: 0, frameCount: 10))
        XCTAssertFalse(AnimatedMascotPlayback.shouldStop(loop: true, frameIndex: 9, frameCount: 10))
    }

    func testOneShotStopsOnLastFrame() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldStop(loop: false, frameIndex: 0, frameCount: 10))
        XCTAssertFalse(AnimatedMascotPlayback.shouldStop(loop: false, frameIndex: 8, frameCount: 10))
        XCTAssertTrue(AnimatedMascotPlayback.shouldStop(loop: false, frameIndex: 9, frameCount: 10))
    }

    func testOneShotWithUnknownFrameCountKeepsPlaying() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldStop(loop: false, frameIndex: 100, frameCount: 0))
    }

    func testFirstRenderRestarts() {
        XCTAssertTrue(AnimatedMascotPlayback.shouldRestart(
            activeData: nil, activeLoop: nil, data: Data([1]), loop: true
        ))
    }

    func testSameDataAndLoopDoesNotRestart() {
        let data = Data([1, 2, 3])
        XCTAssertFalse(AnimatedMascotPlayback.shouldRestart(
            activeData: data, activeLoop: true, data: data, loop: true
        ))
    }

    func testChangedDataRestarts() {
        XCTAssertTrue(AnimatedMascotPlayback.shouldRestart(
            activeData: Data([1]), activeLoop: true, data: Data([2]), loop: true
        ))
    }

    func testChangedLoopRestarts() {
        let data = Data([1])
        XCTAssertTrue(AnimatedMascotPlayback.shouldRestart(
            activeData: data, activeLoop: false, data: data, loop: true
        ))
    }

    func testHoldsFinalFrameWhenOneShotCompletesUninterrupted() {
        XCTAssertTrue(AnimatedMascotPlayback.shouldReapplyHeldFrame(loop: false, superseded: false))
    }

    func testDoesNotHoldFinalFrameWhileLooping() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldReapplyHeldFrame(loop: true, superseded: false))
    }

    func testDoesNotHoldFinalFrameWhenSupersededByNewerRun() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldReapplyHeldFrame(loop: false, superseded: true))
        XCTAssertFalse(AnimatedMascotPlayback.shouldReapplyHeldFrame(loop: true, superseded: true))
    }

    func testCurrentGenerationIsNotSuperseded() {
        XCTAssertFalse(AnimatedMascotPlayback.isSuperseded(generation: 2, activeGeneration: 2))
    }

    func testOlderGenerationIsSuperseded() {
        XCTAssertTrue(AnimatedMascotPlayback.isSuperseded(generation: 1, activeGeneration: 2))
    }

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
}
