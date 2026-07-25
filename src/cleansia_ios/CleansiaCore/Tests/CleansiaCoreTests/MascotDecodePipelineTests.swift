import ImageIO
import UIKit
import UniformTypeIdentifiers
import XCTest
@testable import CleansiaCore

/// End-to-end cover for the progressive decode: a synthetic multi-frame image stands in for the
/// bundled WebP mascots (the data assets live in the app targets, not in this package).
final class MascotDecodePipelineTests: XCTestCase {
    func testFramesArePublishedProgressivelyAndThenComplete() throws {
        let data = try makeAnimation(frameCount: 12)
        var snapshots: [MascotAnimation] = []
        let finished = expectation(description: "complete")

        MascotAssetCache.shared.loadAnimation(name: uniqueName(), data: data) { animation in
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
        let data = try makeAnimation(frameCount: 6)
        let name = uniqueName()
        let finished = expectation(description: "complete")
        MascotAssetCache.shared.loadAnimation(name: name, data: data) { if $0.isComplete { finished.fulfill() } }
        wait(for: [finished], timeout: 5)

        var cached: MascotAnimation?
        MascotAssetCache.shared.loadAnimation(name: name, data: data) { cached = $0 }

        XCTAssertEqual(cached?.frames.count, 6)
        XCTAssertEqual(cached?.isComplete, true)
    }

    func testTwoConcurrentRequestsShareOneDecode() throws {
        let data = try makeAnimation(frameCount: 8)
        let name = uniqueName()
        var first: MascotAnimation?
        var second: MascotAnimation?
        let finished = expectation(description: "complete")
        finished.expectedFulfillmentCount = 2

        MascotAssetCache.shared.loadAnimation(name: name, data: data) {
            first = $0
            if $0.isComplete { finished.fulfill() }
        }
        MascotAssetCache.shared.loadAnimation(name: name, data: data) {
            second = $0
            if $0.isComplete { finished.fulfill() }
        }
        wait(for: [finished], timeout: 5)

        let sharedFrames = zip(first?.frames ?? [], second?.frames ?? []).allSatisfy { $0 === $1 }
        XCTAssertEqual(first?.frames.count, 8)
        XCTAssertEqual(second?.frames.count, 8)
        XCTAssertTrue(sharedFrames, "the second caller must attach to the running decode, not repeat it")
    }

    // MARK: helpers

    private func uniqueName() -> String {
        "mascot_test_\(UUID().uuidString)"
    }

    private func makeAnimation(frameCount: Int) throws -> Data {
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
                kCGImagePropertyGIFDictionary: [kCGImagePropertyGIFDelayTime: 0.05]
            ] as CFDictionary)
        }
        XCTAssertTrue(CGImageDestinationFinalize(destination))
        return data as Data
    }
}
