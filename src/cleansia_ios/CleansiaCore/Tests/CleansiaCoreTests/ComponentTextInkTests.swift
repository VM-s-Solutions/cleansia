import Foundation
import SwiftUI
import XCTest
@testable import CleansiaCore

/// The shared components draw text links and text-button labels in the text ink, not the primary (owner
/// decision 2026-10-05, both apps): the light-mode primary, sky-600, reads 4.10:1 on white, under the 4.5:1
/// floor for text, so they take `CleansiaColors.primaryText` (sky-700; dark mode is the primary's sky-400
/// either way). An icon inside the same control takes its label's ink, so no control shows two blues; fills,
/// borders and standalone icons keep the primary. Read from source, the `AvatarDiscBindingTests` idiom: a
/// rendered colour is not observable in a unit test, the token a view names is.
final class ComponentTextInkTests: XCTestCase {
    /// No Core `Text` names the primary as its own colour. The one exception is the Live Activity's
    /// wordmark fallback: it stands in for the logo, which keeps the brand blue.
    func testNoSharedTextIsDrawnInThePrimary() throws {
        let found = try primaryTexts()

        XCTAssertEqual(found.map(\.file), ["LiveActivityCardViews.swift"], found.map(\.text).joined(separator: "\n"))
        XCTAssertEqual(found.first?.text, "Text(verbatim: \"Cleansia\")")
    }

    /// The icons that share a control with a text-ink label, and the fills and borders that keep the primary.
    func testTheIconsBesideATextInkLabelTakeItAndTheChromeKeepsThePrimary() throws {
        let chip = try compactSource("Components/CleansiaChip.swift")
        XCTAssertTrue(chip.contains(".foregroundColor(isSelected?CleansiaColors.primaryText:CleansiaColors.onSurface)"))
        XCTAssertTrue(chip.contains("isSelected?CleansiaColors.primary.opacity(0.12):CleansiaColors.surface"))
        XCTAssertTrue(chip.contains("isSelected?CleansiaColors.primary:CleansiaColors.outlineVariant"))

        let dropdown = try compactSource("Components/CleansiaDropdown.swift")
        XCTAssertTrue(dropdown.contains("Image(systemName:\"checkmark\").foregroundColor(CleansiaColors.primaryText)"))

        let panel = try compactSource("Components/CleansiaRevealPanel.swift")
        XCTAssertTrue(panel.contains(
            "\"lock.fill\").font(.system(size:13,weight:.semibold)).foregroundColor(CleansiaColors.primaryText)"
        ))
        XCTAssertTrue(panel.contains(
            "Image(systemName:\"chevron.down\").font(.system(size:11,weight:.bold))"
                + ".foregroundColor(CleansiaColors.primaryText)"
        ))

        let consent = try compactSource("Components/CleansiaConsentCheckbox.swift")
        XCTAssertTrue(consent.contains(".tint(CleansiaColors.primaryText)"), "the consent sentence's links")
        XCTAssertTrue(consent.contains("checked?CleansiaColors.primary:CleansiaColors.outline"), "the tick box")

        let activity = try compactSource("LiveActivity/LiveActivityCardViews.swift")
        XCTAssertTrue(activity.contains("?CleansiaColors.primary.opacity(0.22):CleansiaColors.primary)"), "the bar")
    }

    /// A picked chip's label sits on a 12 % primary wash over the card, its lowest ground.
    func testThePickedChipsLabelReadsOnItsWash() {
        let wash = mix(0x0284C7, 0.12, over: 0xFFFFFF)
        XCTAssertGreaterThanOrEqual(contrast(rgb(0x0369A1), wash), 4.5)
        XCTAssertLessThan(contrast(rgb(0x0284C7), wash), 4.5, "the primary is not a text colour on the wash")
    }

    /// Blue text on the primary container reads 4.5:1 in both modes (finding 2026-10-05): in dark mode the
    /// container is sky-700, where the text ink's sky-400 read 2.77:1, so badge text takes
    /// `primaryTextOnContainer` (sky-100 in dark, the text ink's sky-700 in light). The section header's badge
    /// is one.
    func testBadgeTextOnThePrimaryContainerReadsInBothModes() throws {
        for style in [UIUserInterfaceStyle.light, .dark] {
            let container = resolved(CleansiaColors.primaryContainer, style)
            XCTAssertGreaterThanOrEqual(
                contrast(resolved(CleansiaColors.primaryTextOnContainer, style), container),
                4.5
            )
        }
        XCTAssertEqual(
            contrast(
                resolved(CleansiaColors.primaryTextOnContainer, .dark),
                resolved(CleansiaColors.primaryContainer, .dark)
            ),
            5.17,
            accuracy: 0.01
        )
        XCTAssertLessThan(
            contrast(resolved(CleansiaColors.primaryText, .dark), resolved(CleansiaColors.primaryContainer, .dark)),
            4.5,
            "the text ink reads on the dark container after all"
        )
        let light = resolved(CleansiaColors.primaryTextOnContainer, .light)
        XCTAssertEqual(
            contrast(light, resolved(CleansiaColors.primaryText, .light)),
            1,
            accuracy: 0.001,
            "light changed"
        )
        XCTAssertTrue(
            try compactSource("Components/CleansiaSectionHeader.swift")
                .contains(".foregroundColor(CleansiaColors.primaryTextOnContainer).padding(.horizontal,Spacing.s)")
        )
    }

    // MARK: - Helpers

    private typealias RGB = SIMD3<Double>

    private var sources: URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/CleansiaCore")
    }

    /// Every `Text(…)` whose own modifier chain sets a primary foreground.
    private func primaryTexts() throws -> [(file: String, text: String)] {
        let files = try XCTUnwrap(FileManager.default.enumerator(at: sources, includingPropertiesForKeys: nil))
            .compactMap { $0 as? URL }
            .filter { $0.pathExtension == "swift" }
        XCTAssertGreaterThan(files.count, 50, "the Core sources were not found")
        let primary = try NSRegularExpression(pattern: #"CleansiaColors\.primary(?![A-Za-z])"#)
        var found: [(file: String, text: String)] = []
        for file in files {
            let lines = try String(contentsOf: file, encoding: .utf8)
                .components(separatedBy: .newlines)
                .map { $0.trimmingCharacters(in: .whitespaces) }
            for (index, line) in lines.enumerated() where line.hasPrefix("Text(") {
                var depth = parens(line)
                var cursor = index + 1
                while depth > 0, cursor < lines.count {
                    depth += parens(lines[cursor])
                    cursor += 1
                }
                while cursor < lines.count, depth > 0 || lines[cursor].hasPrefix(".") {
                    let modifier = lines[cursor]
                    let range = NSRange(modifier.startIndex..., in: modifier)
                    if modifier.hasPrefix(".foregroundColor("), primary.firstMatch(in: modifier, range: range) != nil {
                        found.append((file.lastPathComponent, line))
                    }
                    depth += parens(modifier)
                    cursor += 1
                }
            }
        }
        return found
    }

    private func compactSource(_ path: String) throws -> String {
        try String(contentsOf: sources.appendingPathComponent(path), encoding: .utf8)
            .components(separatedBy: .whitespacesAndNewlines)
            .joined()
    }

    private func parens(_ line: String) -> Int {
        line.filter { $0 == "(" }.count - line.filter { $0 == ")" }.count
    }

    private func resolved(_ color: Color, _ style: UIUserInterfaceStyle) -> RGB {
        let traits = UITraitCollection(userInterfaceStyle: style)
        var red: CGFloat = 0
        var green: CGFloat = 0
        var blue: CGFloat = 0
        var alpha: CGFloat = 0
        UIColor(color).resolvedColor(with: traits).getRed(&red, green: &green, blue: &blue, alpha: &alpha)
        return RGB(Double(red), Double(green), Double(blue))
    }

    private func rgb(_ hex: UInt32) -> RGB {
        RGB(Double((hex >> 16) & 0xFF), Double((hex >> 8) & 0xFF), Double(hex & 0xFF)) / 255
    }

    private func mix(_ top: UInt32, _ alpha: Double, over bottom: UInt32) -> RGB {
        rgb(top) * alpha + rgb(bottom) * (1 - alpha)
    }

    private func contrast(_ first: RGB, _ second: RGB) -> Double {
        func luminance(_ color: RGB) -> Double {
            let linear = [color.x, color.y, color.z].map { $0 <= 0.04045 ? $0 / 12.92 : pow(($0 + 0.055) / 1.055, 2.4) }
            return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2]
        }
        let (lighter, darker) = (max(luminance(first), luminance(second)), min(luminance(first), luminance(second)))
        return (lighter + 0.05) / (darker + 0.05)
    }
}
