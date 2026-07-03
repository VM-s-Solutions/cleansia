import CleansiaCore
import ImageIO
import UIKit
import XCTest

final class MascotAssetsTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main

    func testEveryMascotImagesetExists() {
        for mascot in Mascot.allCases {
            XCTAssertNotNil(
                UIImage(named: mascot.rawValue, in: appBundle, compatibleWith: nil),
                "Missing imageset \(mascot.rawValue)"
            )
        }
    }

    func testAnimatedMascotDataAssetsLoadAndAnimate() throws {
        for mascot in [AnimatedMascot.cleaningInProgress, .welcoming] {
            let asset = try XCTUnwrap(
                NSDataAsset(name: mascot.rawValue, bundle: appBundle),
                "Missing data asset \(mascot.rawValue)"
            )
            let source = try XCTUnwrap(CGImageSourceCreateWithData(asset.data as CFData, nil))
            XCTAssertGreaterThan(
                CGImageSourceGetCount(source),
                1,
                "\(mascot.rawValue) should decode as a multi-frame animation"
            )
        }
    }

    func testLaunchBackgroundColorExists() {
        XCTAssertNotNil(UIColor(named: "SplashBackground", in: appBundle, compatibleWith: nil))
    }
}
