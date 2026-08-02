import CleansiaCore
import ImageIO
import UIKit
import XCTest

/// Mascot art ships in CleansiaCore's bundle; this asserts that bundle is actually embedded in and
/// readable from the built PARTNER app — the sheet-edge puck degrades to a still image without a
/// word when it isn't, which is indistinguishable from a working static status.
final class MascotAssetsTests: XCTestCase {
    func testEveryStillMascotResolvesInsideThePartnerApp() {
        for mascot in Mascot.allCases {
            XCTAssertNotNil(
                UIImage(named: mascot.rawValue, in: MascotAssets.bundle, compatibleWith: nil),
                "missing imageset \(mascot.rawValue)"
            )
        }
    }

    func testTheInProgressLoopIsAnimatableInsideThePartnerApp() throws {
        var frames = 0
        for name in AnimatedMascot.cleaningInProgress.segmentNames {
            let asset = try XCTUnwrap(
                NSDataAsset(name: name, bundle: MascotAssets.bundle),
                "missing data asset \(name)"
            )
            let source = try XCTUnwrap(CGImageSourceCreateWithData(asset.data as CFData, nil))
            let count = CGImageSourceGetCount(source)
            XCTAssertGreaterThan(count, 1, "\(name) is not a multi-frame animation")
            frames += count
        }
        XCTAssertEqual(frames, 125)
    }

    func testTheInProgressLoopsFirstFramesAreNotTheSamePicture() throws {
        let asset = try XCTUnwrap(
            NSDataAsset(name: AnimatedMascot.cleaningInProgress.rawValue, bundle: MascotAssets.bundle)
        )
        let source = try XCTUnwrap(CGImageSourceCreateWithData(asset.data as CFData, nil))

        let first = try XCTUnwrap(CGImageSourceCreateImageAtIndex(source, 0, nil))
        let second = try XCTUnwrap(CGImageSourceCreateImageAtIndex(source, 1, nil))

        XCTAssertNotEqual(UIImage(cgImage: first).pngData(), UIImage(cgImage: second).pngData())
    }
}
