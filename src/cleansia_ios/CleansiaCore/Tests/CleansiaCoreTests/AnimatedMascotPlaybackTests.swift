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
