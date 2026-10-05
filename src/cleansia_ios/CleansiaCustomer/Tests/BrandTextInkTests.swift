import CleansiaCore
import SwiftUI
import XCTest
@testable import CleansiaCustomer

/// Brand-blue TEXT reads at 4.5:1 or more (finding 2026-10-05). The primary is sky-600 in light mode,
/// 4.10:1 on white and less on every tinted ground, so text takes `CleansiaColors.primaryText`; fills,
/// borders, icons and the shared button components (`CleansiaTextLink`, `CleansiaOutlinedButton`) keep
/// the primary. A text the app draws as its own button label ("See all", "Retry") is text, as on Android.
final class BrandTextInkTests: XCTestCase {
    /// The light grounds the customer app draws blue text on: the card and sheet surface, the page, the
    /// primary container (the default-address and this-device badges, the cleaner's initial), the schedule
    /// form's badge (40 % container), the home-size card (50 % container on the page), the primary tints
    /// under the badges, chips and the picked day part (10–20 %), and the Plus offer's social-proof card.
    func testTheTextInkClearsAAOnEveryLightGroundItIsDrawnOn() {
        let surface = rgb(CleansiaColors.surface)
        let page = rgb(CleansiaColors.background)
        let primary = rgb(CleansiaColors.primary)
        let container = rgb(CleansiaColors.primaryContainer)
        let grounds: [(name: String, ground: RGB)] = [
            ("surface", surface),
            ("page", page),
            ("primary container", container),
            ("schedule badge", over(container, 0.4, surface)),
            ("home size card", over(container, 0.5, page)),
            ("10 % primary", over(primary, 0.10, surface)),
            ("12 % primary", over(primary, 0.12, surface)),
            ("14 % primary", over(primary, 0.14, surface)),
            ("20 % primary", over(primary, 0.20, surface)),
            ("social proof", over(rgb(MembershipPalette.sky400), 0.12, page)),
            ("in-review dispute pill", over(rgb(CleansiaColors.primaryText), 0.14, surface))
        ]
        let ink = rgb(CleansiaColors.primaryText)
        for (name, ground) in grounds {
            XCTAssertGreaterThanOrEqual(contrast(ink, ground), 4.5, name)
        }
        XCTAssertEqual(contrast(ink, surface), 5.93, accuracy: 0.01)
        XCTAssertLessThan(contrast(primary, surface), 4.5, "the primary is not a text colour on white")
    }

    /// No customer `Text` sets the primary as its own colour any more.
    func testNoCustomerTextIsDrawnInThePrimary() throws {
        let found = try primaryTexts()

        XCTAssertTrue(found.isEmpty, found.map { "\($0.file): \($0.text)" }.joined(separator: "\n"))
    }

    /// The texts whose colour comes through a helper rather than their own modifier: the in-review
    /// dispute pill, a picked arrival time, the confirm step's discount and total lines, and the add-address
    /// rows, whose plus glyph keeps the primary from the row.
    func testTheTextsColouredThroughAHelperTakeTheTextInk() throws {
        XCTAssertEqual(DisputeStatusPresentation.color(2), CleansiaColors.primaryText)
        XCTAssertEqual(DisputeStatusPresentation.color(3), CleansiaColors.primaryText)
        let whenWhere = try compactSource("Booking/WhenWhere/WhenWhereStep.swift")
        XCTAssertTrue(whenWhere.contains("privatevartextColor:Color{ifselected{returnCleansiaColors.primaryText}"))
        let confirm = try compactSource("Booking/Confirm/ConfirmStepComponents.swift")
        XCTAssertTrue(confirm.contains("case.success:CleansiaColors.primaryTextcase.total:CleansiaColors.onSurface"))
        XCTAssertTrue(confirm.contains("case.success:CleansiaColors.primaryTextcase.total:CleansiaColors.primaryText"))
        let addRowLabel = ".font(CleansiaTypography.bodyLarge).foregroundColor(CleansiaColors.primaryText)Spacer()"
        for path in [
            "Recurring/CreateRecurringScreen.swift",
            "Booking/WhenWhere/AddressPicker/BookingSavedAddressChooser.swift",
            "Addresses/AddressManagerView.swift"
        ] {
            XCTAssertTrue(try compactSource(path).contains(addRowLabel), path)
        }
    }

    // MARK: - Helpers

    private typealias RGB = SIMD3<Double>

    /// Every `Text(…)` whose own modifier chain sets a primary foreground.
    private func primaryTexts() throws -> [(file: String, text: String)] {
        let sources = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources")
        let files = try XCTUnwrap(FileManager.default.enumerator(at: sources, includingPropertiesForKeys: nil))
            .compactMap { $0 as? URL }
            .filter { $0.pathExtension == "swift" }
        XCTAssertGreaterThan(files.count, 100, "the customer sources were not found")
        let primary = try NSRegularExpression(pattern: #"CleansiaColors\.primary(?![A-Za-z])"#)
        var found: [(file: String, text: String)] = []
        for file in files {
            let lines = try String(contentsOf: file, encoding: .utf8)
                .components(separatedBy: .newlines)
                .map { $0.trimmingCharacters(in: .whitespaces) }
            for (index, line) in lines.enumerated() where line.hasPrefix("Text(") {
                var head = line
                var depth = parens(line)
                var cursor = index + 1
                while depth > 0, cursor < lines.count {
                    head += lines[cursor]
                    depth += parens(lines[cursor])
                    cursor += 1
                }
                while cursor < lines.count, depth > 0 || lines[cursor].hasPrefix(".") {
                    let modifier = lines[cursor]
                    let range = NSRange(modifier.startIndex..., in: modifier)
                    if modifier.hasPrefix(".foregroundColor("), primary.firstMatch(in: modifier, range: range) != nil {
                        found.append((file.lastPathComponent, head))
                    }
                    depth += parens(modifier)
                    cursor += 1
                }
            }
        }
        return found
    }

    private func compactSource(_ path: String) throws -> String {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("Sources/Features")
            .appendingPathComponent(path)
        return try String(contentsOf: url, encoding: .utf8).components(separatedBy: .whitespacesAndNewlines).joined()
    }

    private func parens(_ line: String) -> Int {
        line.filter { $0 == "(" }.count - line.filter { $0 == ")" }.count
    }

    private func rgb(_ color: Color) -> RGB {
        let resolved = UIColor(color).resolvedColor(with: UITraitCollection(userInterfaceStyle: .light))
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
}
