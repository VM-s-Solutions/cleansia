import ImageIO
import UIKit
import UniformTypeIdentifiers
import XCTest
@testable import CleansiaCore

/// End-to-end cover for the progressive decode: a synthetic multi-frame image stands in for the
/// bundled WebP mascots (the data assets live in the app targets, not in this package).
final class MascotDecodePipelineTests: XCTestCase {
    func testFramesArePublishedProgressivelyAndThenComplete() throws {
        let asset = try makeAsset(frameCounts: [12])
        var snapshots: [MascotAnimation] = []
        let finished = expectation(description: "complete")

        MascotAssetCache.shared.loadAnimation(asset) { animation in
            snapshots.append(animation)
            if animation.isComplete { finished.fulfill() }
        }
        wait(for: [finished], timeout: 5)

        XCTAssertGreaterThan(snapshots.count, 1, "playback must start before every frame is decoded")
        XCTAssertEqual(
            snapshots.first?.frames.count,
            AnimatedMascotPlayback.firstChunkSize(frameCount: 12),
            "the first publish is the small first chunk"
        )
        XCTAssertEqual(snapshots.map(\.frames.count), [4, 8, 12])
        XCTAssertEqual(snapshots.last?.frames.count, 12)
        XCTAssertEqual(snapshots.last?.delays.count, 12)
        XCTAssertEqual(snapshots.dropLast().filter(\.isComplete).count, 0, "only the whole loop is complete")
    }

    func testAnAlreadyDecodedAnimationIsServedFromCacheSynchronously() throws {
        let asset = try makeAsset(frameCounts: [6])
        let finished = expectation(description: "complete")
        MascotAssetCache.shared.loadAnimation(asset) { if $0.isComplete { finished.fulfill() } }
        wait(for: [finished], timeout: 5)

        var cached: MascotAnimation?
        MascotAssetCache.shared.loadAnimation(asset) { cached = $0 }

        XCTAssertEqual(cached?.frames.count, 6)
        XCTAssertEqual(cached?.isComplete, true)
    }

    func testTwoConcurrentRequestsShareOneDecode() throws {
        let asset = try makeAsset(frameCounts: [8])
        var first: MascotAnimation?
        var second: MascotAnimation?
        let finished = expectation(description: "complete")
        finished.expectedFulfillmentCount = 2

        MascotAssetCache.shared.loadAnimation(asset) {
            first = $0
            if $0.isComplete { finished.fulfill() }
        }
        MascotAssetCache.shared.loadAnimation(asset) {
            second = $0
            if $0.isComplete { finished.fulfill() }
        }
        wait(for: [finished], timeout: 5)

        let sharedFrames = zip(first?.frames ?? [], second?.frames ?? []).allSatisfy { $0 === $1 }
        XCTAssertEqual(first?.frames.count, 8)
        XCTAssertEqual(second?.frames.count, 8)
        XCTAssertTrue(sharedFrames, "the second caller must attach to the running decode, not repeat it")
    }

    // MARK: segments

    /// The whole point of the split: several files, one continuous loop. Playback must see the frames of
    /// every segment, in segment order, as a single growing buffer with no seam and no restart — and each
    /// frame must keep ITS OWN delay across the seam, which is what makes the concatenation identical to
    /// the original single file.
    func testSegmentsDecodeInOrderIntoOneContinuousAnimation() throws {
        let asset = try makeAsset(frameCounts: [5, 5, 4])
        var snapshots: [MascotAnimation] = []
        let finished = expectation(description: "complete")

        MascotAssetCache.shared.loadAnimation(asset) {
            snapshots.append($0)
            if $0.isComplete { finished.fulfill() }
        }
        wait(for: [finished], timeout: 5)

        let whole = try XCTUnwrap(snapshots.last)
        XCTAssertEqual(whole.frames.count, 14, "the segments concatenate into one animation")
        XCTAssertEqual(
            whole.delays.map { Int(($0 * 100).rounded()) },
            Array(10 ..< 24),
            "every frame keeps its own delay, in concatenated order"
        )
        for (earlier, later) in zip(snapshots, snapshots.dropFirst()) {
            XCTAssertGreaterThan(later.frames.count, earlier.frames.count, "the buffer only grows")
            XCTAssertTrue(
                zip(earlier.frames, later.frames).allSatisfy { $0 === $1 },
                "an appended chunk must extend the buffer, never rebuild it"
            )
        }
    }

    /// The frozen mid-loop hold was the last chunk covering half the animation. Every publish must now
    /// be a small step, so the playhead is never starved for long.
    func testNoPublishStepIsLargerThanTheChunkCap() throws {
        let asset = try makeAsset(frameCounts: [18, 18, 18])
        var counts: [Int] = []
        let finished = expectation(description: "complete")

        MascotAssetCache.shared.loadAnimation(asset) {
            counts.append($0.frames.count)
            if $0.isComplete { finished.fulfill() }
        }
        wait(for: [finished], timeout: 10)

        XCTAssertEqual(counts.last, 54)
        let steps = zip([0] + counts, counts).map { $1 - $0 }
        XCTAssertLessThanOrEqual(steps.max() ?? 0, 8, "publish steps: \(steps)")
    }

    /// Decoded frames are tens of MiB and nothing else ever frees them, so the cache has to let go when
    /// the system asks. After a warning the next request re-decodes instead of answering from memory.
    func testAMemoryWarningDropsTheDecodedFrames() throws {
        let asset = try makeAsset(frameCounts: [6])
        let decoded = expectation(description: "complete")
        MascotAssetCache.shared.loadAnimation(asset) { if $0.isComplete { decoded.fulfill() } }
        wait(for: [decoded], timeout: 5)
        var served: MascotAnimation?
        MascotAssetCache.shared.loadAnimation(asset) { served = $0 }
        XCTAssertEqual(served?.isComplete, true, "a decoded animation is served straight from memory")

        NotificationCenter.default.post(name: UIApplication.didReceiveMemoryWarningNotification, object: nil)
        RunLoop.current.run(until: Date().addingTimeInterval(0.05))

        var afterWarning: MascotAnimation?
        MascotAssetCache.shared.loadAnimation(asset) { afterWarning = $0 }
        XCTAssertNil(afterWarning, "the frames must be gone, not still held")
    }

    // MARK: helpers

    private func makeAsset(frameCounts: [Int]) throws -> MascotAsset {
        var start = 0
        var segments: [Data] = []
        for count in frameCounts {
            try segments.append(makeAnimation(frameCount: count, firstIndex: start))
            start += count
        }
        return MascotAsset(name: "mascot_test_\(UUID().uuidString)", segments: segments, maxPixel: 64)
    }

    /// Frame `n` of the concatenated animation gets a delay of `(10 + n)` cs — a distinct, exactly
    /// representable GIF delay per frame, so decoded order is asserted by timing rather than by pixels.
    private func makeAnimation(frameCount: Int, firstIndex: Int = 0) throws -> Data {
        let data = NSMutableData()
        let destination = try XCTUnwrap(CGImageDestinationCreateWithData(
            data,
            UTType.gif.identifier as CFString,
            frameCount,
            nil
        ))
        CGImageDestinationSetProperties(destination, [
            kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFLoopCount: 0]
        ] as CFDictionary)
        let renderer = UIGraphicsImageRenderer(size: CGSize(width: 8, height: 8))
        for index in 0 ..< frameCount {
            let frame = renderer.image { context in
                UIColor(white: CGFloat(index) / CGFloat(frameCount), alpha: 1).setFill()
                context.fill(CGRect(x: 0, y: 0, width: 8, height: 8))
            }
            try CGImageDestinationAddImage(destination, XCTUnwrap(frame.cgImage), [
                kCGImagePropertyGIFDictionary: [
                    kCGImagePropertyGIFDelayTime: Double(10 + firstIndex + index) / 100.0
                ]
            ] as CFDictionary)
        }
        XCTAssertTrue(CGImageDestinationFinalize(destination))
        return data as Data
    }
}
