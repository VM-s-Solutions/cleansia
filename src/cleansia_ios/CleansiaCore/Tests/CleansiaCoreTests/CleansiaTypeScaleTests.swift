import SwiftUI
import XCTest
@testable import CleansiaCore

/// Poppins draws no Cyrillic, so every Poppins slot must name a Cyrillic-capable counterpart.
/// These assertions are about the *declared* scale and need no registered fonts; the resolved
/// typeface and the glyph coverage that justifies the pairing are asserted in the app test
/// targets, which are the only bundles the `.ttf` files ship in.
final class CleansiaTypeScaleTests: XCTestCase {
    private var slots: [(name: String, style: CleansiaTextStyle)] {
        Mirror(reflecting: CleansiaTypography.scale).children.compactMap { child in
            guard let name = child.label else { return nil }
            return (child.value as? CleansiaTextStyle).map { (name, $0) }
        }
    }

    func testEverySlotInTheScaleIsATextStyle() {
        let declared = Mirror(reflecting: CleansiaTypography.scale).children.count
        XCTAssertGreaterThanOrEqual(declared, 12)
        XCTAssertEqual(slots.count, declared, "a slot that is not a CleansiaTextStyle bypasses the fallback")
    }

    func testEverySlotResolvesToABundledFace() {
        for slot in slots {
            XCTAssertNotNil(
                CleansiaFont.face(slot.style.family, weight: slot.style.weight),
                "\(slot.name) resolves to no bundled face and would render in the system font"
            )
        }
    }

    func testEverySlotWithoutCyrillicNamesACyrillicCapableFallback() {
        for slot in slots {
            guard let face = CleansiaFont.face(slot.style.family, weight: slot.style.weight) else { continue }
            guard face.family.drawsCyrillic == false else { continue }
            guard let fallback = CleansiaFont.glyphFallback(for: face) else {
                XCTFail("\(slot.name) draws no Cyrillic and names no fallback")
                continue
            }
            XCTAssertTrue(
                fallback.family.drawsCyrillic,
                "\(slot.name) falls back to \(fallback.rawValue), which draws no Cyrillic either"
            )
        }
    }

    func testFallbackKeepsTheWeightOrDropsToRegular() {
        XCTAssertEqual(CleansiaFont.glyphFallback(for: .poppinsMedium), .nunitoRegular)
        XCTAssertEqual(CleansiaFont.glyphFallback(for: .poppinsSemiBold), .nunitoSemiBold)
        XCTAssertEqual(CleansiaFont.glyphFallback(for: .poppinsBold), .nunitoBold)
    }

    func testCyrillicCapableFacesTerminateTheChain() {
        for face in CleansiaFont.BundledFace.allCases where face.family.drawsCyrillic {
            XCTAssertNil(CleansiaFont.glyphFallback(for: face), "\(face.rawValue) should end the chain")
        }
    }

    /// `Font.custom(_:size:)` scales itself; a `Font` built from a `CTFont` does not, so the
    /// cascaded path has to reproduce the same curve. These are the sizes SwiftUI resolves an
    /// 18pt custom font to, measured on the iOS 16 floor.
    func testScaledSizeReproducesTheSwiftUICurve() {
        let expected: [DynamicTypeSize: CGFloat] = [
            .xSmall: 16, .small: 16, .medium: 17, .large: 18,
            .xLarge: 20, .xxLarge: 21, .xxxLarge: 24,
            .accessibility1: 28, .accessibility2: 33, .accessibility3: 39,
            .accessibility4: 46, .accessibility5: 51
        ]
        for (size, points) in expected {
            XCTAssertEqual(CleansiaFont.scaledSize(18, for: size), points, "\(size)")
        }
    }

    func testScaledSizeIsMonotonic() {
        let sizes = DynamicTypeSize.allCases.map { CleansiaFont.scaledSize(22, for: $0) }
        XCTAssertEqual(sizes, sizes.sorted())
        XCTAssertEqual(CleansiaFont.scaledSize(22, for: .large), 22)
    }
}
