import ImageIO
import XCTest
@testable import CleansiaCore

/// Reads the SHIPPED segment files out of the customer catalog and checks that they still add up to the
/// animation the playback code assumes. The data assets live in the app target, so the package test
/// bundle reads them from the repo instead — a re-export that drops a frame, renames a file or rounds a
/// delay fails here rather than on a device.
final class MascotSegmentAssetTests: XCTestCase {
    private static let expectedFrames = 125
    private static let expectedDurationMilliseconds = 5207

    private var catalog: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("CleansiaCustomer/Resources/Assets.xcassets")
    }

    func testEverySegmentOfTheCleaningLoopIsPresent() throws {
        for name in AnimatedMascot.cleaningInProgress.segmentNames {
            XCTAssertTrue(
                FileManager.default.fileExists(atPath: dataset(name).path),
                "missing data asset \(name)"
            )
            XCTAssertGreaterThan(try frameDelays(name).count, 1, "\(name) must be a multi-frame animation")
        }
    }

    /// The segments are one animation cut into files: concatenated they must be frame-for-frame and
    /// millisecond-for-millisecond the original 125-frame, 5207 ms loop.
    func testTheSegmentsConcatenateIntoTheWholeLoop() throws {
        let delays = try AnimatedMascot.cleaningInProgress.segmentNames.flatMap { try frameDelays($0) }

        XCTAssertEqual(delays.count, Self.expectedFrames)
        XCTAssertEqual(milliseconds(delays), Self.expectedDurationMilliseconds)
    }

    /// The 63-frame asset this replaced ran at 83/84 ms with ONE stray 41 ms frame, which is what made the
    /// old rate-matched display link quantize and judder. Every frame here is one 24 fps beat.
    func testEveryFrameIsAWholeTwentyFourFpsBeat() throws {
        let delays = try AnimatedMascot.cleaningInProgress.segmentNames.flatMap { try frameDelays($0) }

        for (index, delay) in delays.enumerated() {
            XCTAssertEqual(delay, 1.0 / 24.0, accuracy: 0.001, "frame \(index) is off the 24 fps beat")
        }
    }

    /// Segments must be evenly sized: the decode cost of a segment is quadratic in ITS frame count, so one
    /// fat segment would reintroduce the stall the split exists to remove.
    func testSegmentsAreEvenlySized() throws {
        let counts = try AnimatedMascot.cleaningInProgress.segmentNames.map { try frameDelays($0).count }

        XCTAssertEqual(counts.reduce(0, +), Self.expectedFrames)
        XCTAssertLessThanOrEqual((counts.max() ?? 0) - (counts.min() ?? 0), 1, "counts: \(counts)")
    }

    /// The whole point, end to end: with the SHIPPED segment sizes and the SHIPPED chunk plan, every frame
    /// is decoded before the playhead asks for it — no mid-loop freeze — with room for a much slower
    /// device than the one the cost was measured on.
    func testEveryFrameIsDecodedBeforePlaybackNeedsIt() throws {
        let counts = try AnimatedMascot.cleaningInProgress.segmentNames.map { try frameDelays($0).count }
        let delays = try AnimatedMascot.cleaningInProgress.segmentNames.flatMap { try frameDelays($0) }
        let availability = decodeSchedule(counts: counts)

        XCTAssertEqual(availability.count, delays.count)
        XCTAssertLessThan(availability[0], 0.05, "the first frames must land within a few display frames")
        for slowdown in [1.0, 3.0] {
            var needed = availability[0] * slowdown
            for index in delays.indices {
                XCTAssertGreaterThanOrEqual(
                    needed - availability[index] * slowdown,
                    0,
                    "frame \(index) is late at \(slowdown)x the measured decode cost"
                )
                needed += delays[index]
            }
        }
    }

    func testTheWelcomingMascotIsStillOneWholeFile() throws {
        let delays = try frameDelays(AnimatedMascot.welcoming.rawValue)

        XCTAssertEqual(AnimatedMascot.welcoming.segmentNames.count, 1)
        XCTAssertGreaterThan(delays.count, 1)
    }

    // MARK: helpers

    /// ImageIO replays a file from frame 0 for every frame it is asked for, so decoding local frame `i`
    /// costs `replayCost * (i + 1)`. That constant comes out at ~0.54 ms across every measured shape
    /// (125x1 = 4263 ms, 63x1 = 1170 ms, 5x25 = 871 ms, 7x18 = 637 ms, 9x14 = 517 ms on the simulator),
    /// which is what makes this model worth asserting against.
    private static let replayCost: TimeInterval = 0.00054

    /// When each frame reaches the screen: walk the real chunk plan, charging each chunk its replay cost
    /// and releasing frames only when the real publish rule says so.
    private func decodeSchedule(counts: [Int]) -> [TimeInterval] {
        let total = counts.reduce(0, +)
        let firstChunk = AnimatedMascotPlayback.firstChunkSize(frameCount: total)
        var availability = [TimeInterval](repeating: .infinity, count: total)
        var clock: TimeInterval = 0
        var cursor = 0
        var published = 0
        while let slice = AnimatedMascotPlayback.nextChunkSlice(
            published: published, cursor: cursor, counts: counts, firstChunk: firstChunk
        ) {
            clock += slice.range.reduce(0) { $0 + Self.replayCost * TimeInterval($1 + 1) }
            cursor += slice.range.count
            guard AnimatedMascotPlayback.shouldPublish(
                decoded: cursor, published: published, isComplete: cursor >= total, firstChunk: firstChunk
            ) else { continue }
            for frame in published ..< cursor {
                availability[frame] = clock
            }
            published = cursor
        }
        return availability
    }

    private func dataset(_ name: String) -> URL {
        catalog.appendingPathComponent("\(name).dataset/\(name).webp")
    }

    private func frameDelays(_ name: String) throws -> [TimeInterval] {
        let data = try Data(contentsOf: dataset(name))
        let source = try XCTUnwrap(CGImageSourceCreateWithData(data as CFData, nil))
        return try (0 ..< CGImageSourceGetCount(source)).map { index in
            let properties = try XCTUnwrap(
                CGImageSourceCopyPropertiesAtIndex(source, index, nil) as? [CFString: Any]
            )
            let webp = try XCTUnwrap(properties[kCGImagePropertyWebPDictionary] as? [CFString: Any])
            return try XCTUnwrap(webp[kCGImagePropertyWebPUnclampedDelayTime] as? Double)
        }
    }

    /// WebP stores each delay as whole milliseconds, so each frame is rounded back before summing.
    private func milliseconds(_ delays: [TimeInterval]) -> Int {
        delays.reduce(0) { $0 + Int(($1 * 1000).rounded()) }
    }
}
