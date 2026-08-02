import UIKit
import XCTest
@testable import CleansiaCore

/// The SHIPPED bytes through the PRODUCTION resolution path, in the bundle both apps read from.
///
/// `AnimatedMascotView` falls back to a still image whenever a data asset cannot be resolved, so a
/// mascot that stopped animating renders happily and looks finished. Checking that the files exist
/// misses that entirely — the asset has to come back from `MascotAssetCache`, decode, and produce
/// frames that differ from one another.
final class MascotBundledAssetTests: XCTestCase {
    func testEveryStillMascotResolvesFromTheSharedBundle() {
        for mascot in Mascot.allCases {
            XCTAssertNotNil(
                UIImage(named: mascot.rawValue, in: MascotAssets.bundle, compatibleWith: nil),
                "missing imageset \(mascot.rawValue)"
            )
        }
    }

    func testEveryAnimatedMascotResolvesAllOfItsSegments() throws {
        for mascot in [AnimatedMascot.cleaningInProgress, .welcoming] {
            let asset = try XCTUnwrap(
                MascotAssetCache.shared.asset(for: mascot),
                "\(mascot.rawValue) does not resolve from the shared bundle"
            )
            XCTAssertEqual(
                asset.segments.count,
                mascot.segmentNames.count,
                "\(mascot.rawValue) resolved only \(asset.segments.count) of its segments"
            )
        }
    }

    func testTheCleaningLoopDecodesIntoFramesThatActuallyMove() throws {
        let animation = try decode(.cleaningInProgress)

        XCTAssertEqual(animation.frames.count, 125)
        XCTAssertTrue(animation.isComplete)
        assertFramesDiffer(animation)
    }

    func testTheWelcomingMascotDecodesIntoFramesThatActuallyMove() throws {
        let animation = try decode(.welcoming)

        XCTAssertGreaterThan(animation.frames.count, 1)
        XCTAssertTrue(animation.isComplete)
        assertFramesDiffer(animation)
    }

    /// The decoded loop, driven through the real playback view on a synthetic clock: the last link
    /// between "bytes decode" and "the puck visibly changes".
    func testThePlaybackViewPaintsSuccessiveFramesOfTheShippedLoop() throws {
        let animation = try decode(.welcoming)
        let view = MascotAnimationView()
        var now: TimeInterval = 0
        view.currentTime = { now }
        view.makeTicker = { _ in NoopTicker() }
        view.prepare(poster: nil, loop: true)
        view.update(animation)

        let first = view.image
        now += animation.delays.prefix(1).reduce(0, +) + 0.001
        view.step(now: now)
        let second = view.image

        XCTAssertNotNil(first)
        XCTAssertFalse(first === second, "the playhead never left frame 0")
        XCTAssertNotEqual(first?.pngData(), second?.pngData())
    }

    // MARK: - Helpers

    private func decode(_ mascot: AnimatedMascot) throws -> MascotAnimation {
        let asset = try XCTUnwrap(MascotAssetCache.shared.asset(for: mascot))
        var decoded: MascotAnimation?
        let complete = expectation(description: "\(mascot.rawValue) decoded")
        MascotAssetCache.shared.loadAnimation(asset) { animation in
            decoded = animation
            if animation.isComplete { complete.fulfill() }
        }
        wait(for: [complete], timeout: 60)
        return try XCTUnwrap(decoded)
    }

    /// A still image bundled as a "loop" would decode into n identical frames and pass every count
    /// assertion above, so the pixels have to be compared.
    private func assertFramesDiffer(_ animation: MascotAnimation) {
        let first = animation.frames.first?.pngData()
        let middle = animation.frames[animation.frames.count / 2].pngData()
        let last = animation.frames.last?.pngData()

        XCTAssertNotNil(first)
        XCTAssertNotEqual(first, middle)
        XCTAssertNotEqual(middle, last)
    }
}

private final class NoopTicker: MascotTicker {
    var isPaused = false
    func invalidate() {}
}
