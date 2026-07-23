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

    // MARK: animationRepeatCount

    func testLoopingRepeatsForever() {
        XCTAssertEqual(AnimatedMascotPlayback.animationRepeatCount(loop: true), 0)
    }

    func testOneShotPlaysOnce() {
        XCTAssertEqual(AnimatedMascotPlayback.animationRepeatCount(loop: false), 1)
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
        XCTAssertTrue(AnimatedMascotPlayback.shouldResumePlayback(hasWindow: true, hasFrames: true, isAnimating: false))
    }

    func testDoesNotResumeOffWindow() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldResumePlayback(
            hasWindow: false,
            hasFrames: true,
            isAnimating: false
        ))
    }

    func testDoesNotResumeWithoutFrames() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldResumePlayback(
            hasWindow: true,
            hasFrames: false,
            isAnimating: false
        ))
    }

    func testDoesNotResumeWhileAlreadyAnimating() {
        XCTAssertFalse(AnimatedMascotPlayback.shouldResumePlayback(hasWindow: true, hasFrames: true, isAnimating: true))
    }

    // MARK: totalDuration

    func testTotalDurationUsesSummedDelaysWhenPresent() {
        XCTAssertEqual(AnimatedMascotPlayback.totalDuration(summedDelays: 2.5, frameCount: 50), 2.5)
    }

    func testTotalDurationFallsBackTo30fpsWhenNoDelays() {
        XCTAssertEqual(AnimatedMascotPlayback.totalDuration(summedDelays: 0, frameCount: 60), 2.0, accuracy: 0.0001)
    }

    func testTotalDurationAvoidsZeroForEmptyFrames() {
        XCTAssertEqual(
            AnimatedMascotPlayback.totalDuration(summedDelays: 0, frameCount: 0),
            1.0 / 30.0,
            accuracy: 0.0001
        )
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
}
