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
@MainActor
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

    /// A focused field's floating label is text: it takes the text ink, 5.93:1 on the field's white where the
    /// primary read 4.10:1 (finding 2026-10-05); the focus border and the caret keep the primary.
    func testAFocusedFieldsLabelTakesTheTextInkAndItsBorderKeepsThePrimary() throws {
        for path in ["Components/CleansiaTextField.swift", "Components/CleansiaPhoneInput.swift"] {
            let field = try compactSource(path)
            XCTAssertTrue(
                field.contains("privatevarfloatingLabelColor:Color{")
                    && field.contains("returnfocused?CleansiaColors.primaryText:CleansiaColors.onSurfaceVariant}"),
                "\(path): the focused label"
            )
            XCTAssertTrue(
                field.contains("returnfocused?CleansiaColors.primary:CleansiaColors.outline}"),
                "\(path): the focus border"
            )
        }
        XCTAssertGreaterThanOrEqual(contrast(rgb(0x0369A1), rgb(0xFFFFFF)), 4.5)
        XCTAssertLessThan(contrast(rgb(0x0284C7), rgb(0xFFFFFF)), 4.5, "the primary reads on the field after all")
    }

    /// A picked chip's label sits on a 12 % primary wash over the card, its lowest ground.
    func testThePickedChipsLabelReadsOnItsWash() {
        let wash = mix(0x0284C7, 0.12, over: 0xFFFFFF)
        XCTAssertGreaterThanOrEqual(contrast(rgb(0x0369A1), wash), 4.5)
        XCTAssertLessThan(contrast(rgb(0x0284C7), wash), 4.5, "the primary is not a text colour on the wash")
    }

    /// An unticked box is a control's edge, so it reads 3:1 on the surface in both modes: the outline it was drawn in
    /// read about 1.2:1 on white.
    func testAnUntickedCheckboxReadsOnTheSurface() throws {
        let checkbox = try compactSource("Components/CleansiaCheckbox.swift")
        XCTAssertTrue(checkbox.contains(".foregroundColor(checked?CleansiaColors.primary:CleansiaColors.quiet)"))
        XCTAssertLessThan(
            contrast(resolved(CleansiaColors.outline, .light), resolved(CleansiaColors.surface, .light)),
            1.3
        )
        for style in [UIUserInterfaceStyle.light, .dark] {
            XCTAssertGreaterThanOrEqual(
                contrast(resolved(CleansiaColors.quiet, style), resolved(CleansiaColors.surface, style)),
                3,
                "\(style == .dark ? "dark" : "light")"
            )
        }
    }

    /// The partner Orders board's three tokens: the set-off band, the quiet ink and the crew-mate's disc, each an
    /// existing palette value per mode (the crew-mate's dark disc is green-400 at 16 % over the surface).
    func testTheBoardTokensResolveToTheirPaletteValuesInBothModes() {
        XCTAssertEqual(resolved(CleansiaColors.dayLineSetOff, .light), rgb(0xBAE6FD))
        XCTAssertEqual(resolved(CleansiaColors.dayLineSetOff, .dark), rgb(0x0C4A6E))
        XCTAssertEqual(resolved(CleansiaColors.quiet, .light), rgb(0x64748B))
        XCTAssertEqual(resolved(CleansiaColors.quiet, .dark), rgb(0x94A3B8))
        XCTAssertEqual(resolved(CleansiaColors.crewMateContainer, .light), rgb(0xDCFCE7))
        let disc = composited(CleansiaColors.crewMateContainer, over: CleansiaColors.surface, .dark)
        for (drawn, wanted) in zip([disc.x, disc.y, disc.z], [0x25, 0x46, 0x46]) {
            XCTAssertEqual(drawn * 255, Double(wanted), accuracy: 1.5, "green-400 at 16 % over the dark surface")
        }
        XCTAssertGreaterThanOrEqual(contrast(resolved(CleansiaColors.successInk, .light), rgb(0xDCFCE7)), 4.5)
        XCTAssertGreaterThanOrEqual(contrast(resolved(CleansiaColors.successInk, .dark), disc), 4.5)
        XCTAssertGreaterThanOrEqual(contrast(resolved(CleansiaColors.quiet, .light), rgb(0xFFFFFF)), 4.5)
        XCTAssertGreaterThanOrEqual(contrast(resolved(CleansiaColors.quiet, .dark), rgb(0x1E293B)), 4.5)
    }

    /// A filled button's label reads 4.5:1 on its dark-mode fill (finding 2026-10-05): sky-900 on the dark
    /// primary's sky-400 read 4.42:1, so dark mode takes sky-950 (6.48:1). Light mode is unchanged, white.
    func testTheFilledButtonsLabelReadsOnItsDarkFill() throws {
        XCTAssertEqual(
            contrast(resolved(CleansiaColors.onPrimary, .dark), resolved(CleansiaColors.primary, .dark)),
            6.48,
            accuracy: 0.01
        )
        XCTAssertLessThan(contrast(rgb(0x0C4A6E), rgb(0x38BDF8)), 4.5, "sky-900 reads on sky-400 after all")
        XCTAssertEqual(contrast(resolved(CleansiaColors.onPrimary, .light), rgb(0xFFFFFF)), 1, accuracy: 0.001)
        let button = try compactSource("Components/CleansiaButton.swift")
        XCTAssertTrue(button.contains(
            ".foregroundColor(Self.ink(pressable:pressable,disabled:disabledContentColor))"
                + ".background(Self.fill(pressable:pressable,disabled:disabledContainerColor))"
        ))
        XCTAssertEqual(CleansiaPrimaryButton.ink(pressable: true, disabled: nil), CleansiaColors.onPrimary)
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

    /// An icon on the primary container reads 3:1 or more in both modes (finding 2026-10-05): in dark mode the
    /// container is sky-700, where the primary's sky-400 read 2.77:1, so icons on it take
    /// `primaryIconOnContainer` (sky-100 in dark, 5.17:1; the primary itself in light, 3.57:1, unchanged).
    func testIconsOnThePrimaryContainerReadInBothModes() {
        for style in [UIUserInterfaceStyle.light, .dark] {
            XCTAssertGreaterThanOrEqual(
                contrast(
                    resolved(CleansiaColors.primaryIconOnContainer, style),
                    resolved(CleansiaColors.primaryContainer, style)
                ),
                3
            )
        }
        XCTAssertEqual(
            contrast(
                resolved(CleansiaColors.primaryIconOnContainer, .dark),
                resolved(CleansiaColors.primaryContainer, .dark)
            ),
            5.17,
            accuracy: 0.01
        )
        XCTAssertLessThan(
            contrast(resolved(CleansiaColors.primary, .dark), resolved(CleansiaColors.primaryContainer, .dark)),
            3,
            "the primary reads on the dark container after all"
        )
        XCTAssertEqual(
            contrast(resolved(CleansiaColors.primaryIconOnContainer, .light), resolved(CleansiaColors.primary, .light)),
            1,
            accuracy: 0.001,
            "light changed"
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
        StatusBarFade.rgb(color, style == .dark ? .dark : .light)
    }

    private func composited(_ top: Color, over bottom: Color, _ style: UIUserInterfaceStyle) -> RGB {
        let scheme: ColorScheme = style == .dark ? .dark : .light
        let renderer = ImageRenderer(
            content: ZStack { bottom
                top
            }.frame(width: 1, height: 1).environment(\.colorScheme, scheme)
        )
        renderer.scale = 1
        guard let image = renderer.cgImage,
              let space = CGColorSpace(name: CGColorSpace.sRGB),
              let context = CGContext(
                  data: nil,
                  width: 1,
                  height: 1,
                  bitsPerComponent: 8,
                  bytesPerRow: 4,
                  space: space,
                  bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
              )
        else { return .zero }
        context.draw(image, in: CGRect(x: 0, y: 0, width: 1, height: 1))
        guard let pixel = context.data?.assumingMemoryBound(to: UInt8.self) else { return .zero }
        return RGB(Double(pixel[0]), Double(pixel[1]), Double(pixel[2])) / 255
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
