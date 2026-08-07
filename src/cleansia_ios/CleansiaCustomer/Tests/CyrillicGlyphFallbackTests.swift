import CoreText
import SwiftUI
import XCTest
@testable import CleansiaCore

/// CoreText itemizes a line across a font's cascade **per glyph**, so the brand face keeps every
/// character it can draw and only the uncovered ones move. These assertions read the itemization
/// back out of a laid-out `CTLine` — the same object `Text` renders from.
final class CyrillicGlyphFallbackTests: XCTestCase {
    private let mixed = "Anna Анна Smith"

    private func itemize(_ text: String, _ font: UIFont) -> [(text: String, face: String)] {
        let attributed = NSAttributedString(string: text, attributes: [.font: font])
        let line = CTLineCreateWithAttributedString(attributed)
        guard let runs = CTLineGetGlyphRuns(line) as? [CTRun] else { return [] }
        return runs.compactMap { run in
            let attributes = CTRunGetAttributes(run) as NSDictionary
            guard let runFont = attributes[kCTFontAttributeName] as CFTypeRef? else { return nil }
            let range = CTRunGetStringRange(run)
            let slice = (text as NSString).substring(with: NSRange(location: range.location, length: range.length))
            // swiftlint:disable:next force_cast
            return (slice, CTFontCopyPostScriptName(runFont as! CTFont) as String)
        }
    }

    private func style(_ name: String) throws -> CleansiaTextStyle {
        let slot = Mirror(reflecting: CleansiaTypography.scale).children.first { $0.label == name }
        return try XCTUnwrap(slot?.value as? CleansiaTextStyle, name)
    }

    func testCyrillicInsideALatinHeadingKeepsPoppinsForTheLatin() throws {
        let font = try style("headlineSmall").uiFont(for: .large)
        let runs = itemize(mixed, font)

        XCTAssertEqual(runs.map(\.text), ["Anna ", "Анна", " Smith"])
        XCTAssertEqual(runs.map(\.face), ["Poppins-SemiBold", "Nunito-SemiBold", "Poppins-SemiBold"])
    }

    func testWithoutTheCascadeTheSameHeadingLeavesTheBrandFamily() throws {
        let bare = try XCTUnwrap(UIFont(name: "Poppins-SemiBold", size: 18))
        let faces = Set(itemize(mixed, bare).map(\.face))

        XCTAssertFalse(faces.contains("Nunito-SemiBold"))
        XCTAssertTrue(faces.contains { $0.hasPrefix("Poppins") == false }, "expected a system fallback face")
    }

    func testTheCascadeDoesNotCaptureScriptsTheSystemHandles() throws {
        let font = try style("headlineSmall").uiFont(for: .large)
        let faces = itemize("A 😀 漢", font).map(\.face)

        XCTAssertTrue(faces.contains("Poppins-SemiBold"))
        XCTAssertFalse(faces.contains("Nunito-SemiBold"), "emoji and CJK must stay on the system chain")
        XCTAssertTrue(faces.contains("AppleColorEmoji"))
    }

    func testEverySlotWithoutCyrillicResolvesToItsBrandFaceAndCascadesToNunito() throws {
        for child in Mirror(reflecting: CleansiaTypography.scale).children {
            guard let name = child.label, let style = child.value as? CleansiaTextStyle else { continue }
            let font = style.uiFont(for: .large)
            let face = try XCTUnwrap(CleansiaFont.face(style.family, weight: style.weight), name)

            XCTAssertEqual(font.fontName, face.rawValue, name)

            let cascade = CTFontCopyAttribute(font as CTFont, kCTFontCascadeListAttribute) as? [CTFontDescriptor]
            guard let fallback = CleansiaFont.glyphFallback(for: face) else {
                XCTAssertNil(cascade, "\(name) draws every script and needs no cascade")
                continue
            }
            let first = try XCTUnwrap(cascade?.first, "\(name) has no glyph fallback")
            XCTAssertEqual(
                CTFontDescriptorCopyAttribute(first, kCTFontNameAttribute) as? String,
                fallback.rawValue,
                name
            )
        }
    }

    func testCyrillicIsDrawnAtTheSlotWeight() throws {
        let expected = [
            "displayLarge": "Nunito-Bold", "displayMedium": "Nunito-Bold",
            "headlineLarge": "Nunito-SemiBold", "headlineMedium": "Nunito-SemiBold",
            "headlineSmall": "Nunito-SemiBold"
        ]
        for (name, face) in expected {
            let runs = try itemize("Анна", style(name).uiFont(for: .large))
            XCTAssertEqual(runs.map(\.face), [face], name)
        }
    }

    func testScalingKeepsTheCascade() throws {
        for size in DynamicTypeSize.allCases {
            let font = try style("headlineSmall").uiFont(for: size)
            XCTAssertEqual(itemize("Анна", font).map(\.face), ["Nunito-SemiBold"], "\(size)")
            XCTAssertEqual(font.pointSize, CleansiaFont.scaledSize(18, for: size), "\(size)")
        }
    }
}
