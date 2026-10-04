import CleansiaCore
import SwiftUI
import XCTest
@testable import CleansiaCustomer

/// A service a chosen package already books must read as covered at a glance (owner remark 2026-10-04:
/// the grey caption under the name was almost invisible): the row wears a primary tint and a primary
/// border, and the note is a badge whose text stays legible on it in both schemes.
final class InPackageMarkerLookTests: XCTestCase {
    private static let components = "CleansiaCustomer/Sources/Features/Booking/Steps/ServicesStepComponents.swift"
    private static let recurring = "CleansiaCustomer/Sources/Features/Recurring/CreateRecurringScreen.swift"

    func testTheCoveredRowAndTheBadgeUseTheReferenceTints() {
        XCTAssertEqual(InPackageStyle.rowTintOpacity(.light), 0.08)
        XCTAssertEqual(InPackageStyle.rowTintOpacity(.dark), 0.16)
        XCTAssertEqual(InPackageStyle.badgeOpacity(.light), 0.14)
        XCTAssertEqual(InPackageStyle.badgeOpacity(.dark), 0.24)
        XCTAssertEqual(InPackageStyle.borderWidth, 1.5)
    }

    /// WCAG AA for the badge text on the badge, over a covered row's tint and over the booking's picked
    /// fill, which replaces the tint.
    func testTheBadgeTextMeetsAAOnTheBadgeInBothSchemes() {
        for style in [UIUserInterfaceStyle.light, .dark] {
            let scheme: ColorScheme = style == .dark ? .dark : .light
            let primary = rgb(CleansiaColors.primary, style)
            let surface = rgb(CleansiaColors.surface, style)
            let covered = over(primary, InPackageStyle.rowTintOpacity(scheme), surface)
            let picked = over(rgb(CleansiaColors.primaryContainer, style), 0.5, surface)
            for card in [covered, picked] {
                let badge = over(primary, InPackageStyle.badgeOpacity(scheme), card)
                XCTAssertGreaterThanOrEqual(contrast(rgb(InPackageStyle.ink, style), badge), 4.5, "\(style)")
            }
        }
    }

    /// The badge is a check on a primary tint in the ink, no longer a grey caption after a box glyph.
    func testTheNoteIsABadge() throws {
        let source = try compactSource(Self.components)
        XCTAssertTrue(source.contains("Image(systemName:\"checkmark.circle.fill\")"))
        XCTAssertFalse(source.contains("shippingbox"), "the box glyph is back")
        XCTAssertEqual(source.components(separatedBy: ".foregroundColor(InPackageStyle.ink)").count - 1, 2)
        XCTAssertTrue(source.contains(
            ".background(CleansiaColors.primary.opacity(InPackageStyle.badgeOpacity(colorScheme)),"
        ))
    }

    /// Both lists that mark a covered service tint the row and draw the covered border.
    func testBothServiceListsDrawTheCoveredRow() throws {
        for path in [Self.components, Self.recurring] {
            let source = try compactSource(path)
            XCTAssertTrue(source.contains("covered?InPackageStyle.rowTint(colorScheme):.clear"), path)
            XCTAssertTrue(source.contains("returncovered?InPackageStyle.border:CleansiaColors.outlineVariant"), path)
            XCTAssertTrue(source.contains("InPackageStyle.borderWidth"), path)
            XCTAssertTrue(source.contains("InPackageNote(text:inPackageNote)"), path)
        }
    }

    // MARK: - Helpers

    private typealias RGB = SIMD3<Double>

    private func rgb(_ color: Color, _ style: UIUserInterfaceStyle) -> RGB {
        let resolved = UIColor(color).resolvedColor(with: UITraitCollection(userInterfaceStyle: style))
        var red: CGFloat = 0
        var green: CGFloat = 0
        var blue: CGFloat = 0
        var alpha: CGFloat = 0
        resolved.getRed(&red, green: &green, blue: &blue, alpha: &alpha)
        return RGB(Double(red), Double(green), Double(blue))
    }

    private func over(_ top: RGB, _ alpha: Double, _ bottom: RGB) -> RGB {
        top * alpha + bottom * (1 - alpha)
    }

    private func contrast(_ first: RGB, _ second: RGB) -> Double {
        func luminance(_ color: RGB) -> Double {
            let linear = [color.x, color.y, color.z].map { $0 <= 0.04045 ? $0 / 12.92 : pow(($0 + 0.055) / 1.055, 2.4) }
            return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2]
        }
        let (lighter, darker) = (max(luminance(first), luminance(second)), min(luminance(first), luminance(second)))
        return (lighter + 0.05) / (darker + 0.05)
    }

    private func compactSource(_ path: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let source = try String(contentsOf: root.appendingPathComponent(path), encoding: .utf8)
        return source.components(separatedBy: .whitespacesAndNewlines).joined()
    }
}
