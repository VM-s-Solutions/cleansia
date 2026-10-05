import CleansiaCore
import SwiftUI
import XCTest
@testable import CleansiaCustomer

/// Brand-blue TEXT reads at 4.5:1 or more (finding 2026-10-05). The primary is sky-600 in light mode,
/// 4.10:1 on white and less on every tinted ground, so text takes `CleansiaColors.primaryText`; fills,
/// borders, filled buttons and standalone icons keep the primary. A text link or text button — the app's
/// own ("See all", "Retry") and the shared ones (`CleansiaTextLink`) — is text, and an icon inside the same
/// control takes the same ink as its label (owner decision 2026-10-05: one ink per control).
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
    /// rows, whose label and plus glyph take the ink from the row.
    func testTheTextsColouredThroughAHelperTakeTheTextInk() throws {
        XCTAssertEqual(DisputeStatusPresentation.color(2), CleansiaColors.primaryText)
        XCTAssertEqual(DisputeStatusPresentation.color(3), CleansiaColors.primaryText)
        let whenWhere = try compactSource("Booking/WhenWhere/WhenWhereStep.swift")
        XCTAssertTrue(whenWhere.contains("privatevartextColor:Color{ifselected{returnCleansiaColors.primaryText}"))
        let confirm = try compactSource("Booking/Confirm/ConfirmStepComponents.swift")
        XCTAssertTrue(confirm.contains("case.success:CleansiaColors.primaryTextcase.total:CleansiaColors.onSurface"))
        XCTAssertTrue(confirm.contains("case.success:CleansiaColors.primaryTextcase.total:CleansiaColors.primaryText"))
        let addRowLabel = ".font(CleansiaTypography.bodyLarge)Spacer()}.foregroundColor(CleansiaColors.primaryText)"
        for path in [
            "Recurring/CreateRecurringScreen.swift",
            "Booking/WhenWhere/AddressPicker/BookingSavedAddressChooser.swift",
            "Addresses/AddressManagerView.swift"
        ] {
            XCTAssertTrue(try compactSource(path).contains(addRowLabel), path)
        }
    }

    /// The text buttons whose ink comes from a tint or a style, and the icons inside them (owner decision
    /// 2026-10-05): the shell's tint (the selected tab's icon and label, the back buttons), the toolbars'
    /// text buttons, the schedule card's Edit and Pause/Resume, the photos row's chevron and the add-address
    /// rows' plus glyph, which share their label's button.
    func testTheTextButtonsAndTheirIconsTakeTheTextInk() throws {
        let shell = try compactSource("Shell/CustomerShellView.swift")
        XCTAssertTrue(shell.contains(".tint(CleansiaColors.primaryText)"), "the tab bar's tint")
        XCTAssertFalse(shell.contains(".tint(CleansiaColors.primary)"))
        XCTAssertTrue(
            try compactSource("Home/NotificationsInboxSheet.swift")
                .contains("Button(L10n.NotificationsInbox.close){dismiss()}.tint(CleansiaColors.primaryText)")
        )
        XCTAssertTrue(
            try compactSource("Disputes/DisputesListView.swift")
                .contains("systemImage:\"plus\")}.tint(CleansiaColors.primaryText)")
        )
        let cards = try compactSource("Recurring/RecurringBookingsScreen.swift")
        XCTAssertTrue(cards.contains("systemImage:\"square.and.pencil\",tint:CleansiaColors.primaryText,"))
        XCTAssertTrue(cards.contains("\"play.circle\",tint:CleansiaColors.primaryText,"))
        XCTAssertTrue(
            try compactSource("Orders/OrderDetailPhotos.swift").contains(
                "Image(systemName:\"chevron.right\").font(.system(size:12,weight:.semibold))"
                    + ".foregroundColor(CleansiaColors.primaryText)"
            )
        )
        for path in [
            "Recurring/CreateRecurringScreen.swift",
            "Booking/WhenWhere/AddressPicker/BookingSavedAddressChooser.swift",
            "Addresses/AddressManagerView.swift"
        ] {
            XCTAssertTrue(
                try compactSource(path).contains("Image(systemName:\"plus\")Text("),
                "\(path): the plus glyph no longer shares its label's row"
            )
        }
    }

    /// A sheet takes its environment from where `.sheet` is attached, so the shell's tint sits after its two
    /// sheets. Set before them, it never reached the booking sheet or the address manager, and the
    /// preferred-cleaner list the booking sheet opens drew its toolbar Back in iOS's own blue, 4.0:1 on white
    /// (finding 2026-10-05). That Back names no ink of its own; it takes the shell's.
    func testTheShellsSheetsAndTheirTextButtonsSitInsideItsTint() throws {
        let shell = try compactSource("Shell/CustomerShellView.swift")
        let tint = try XCTUnwrap(shell.range(of: ".tint(CleansiaColors.primaryText)"))
        for sheet in [
            ".sheet(isPresented:$model.isBookingPresented",
            ".sheet(isPresented:$model.isAddressManagerPresented"
        ] {
            let attached = try XCTUnwrap(shell.range(of: sheet), sheet)
            XCTAssertLessThan(attached.lowerBound, tint.lowerBound, "\(sheet) is attached outside the tint")
        }
        XCTAssertTrue(
            try compactSource("Booking/Confirm/ConfirmExtrasComponents.swift")
                .contains("ToolbarItem(placement:.cancellationAction){Button(L10n.Booking.back,action:onDismiss)}"),
            "the preferred-cleaner Back"
        )
    }

    /// Dark mode: on the primary container (sky-700 there) the text ink's sky-400 read 2.77:1, so the
    /// default-address badges, the "This device" chip and the cleaner's initial take the container's text ink,
    /// sky-100 in dark (5.17:1) and unchanged in light (finding 2026-10-05).
    func testTheBadgesOnThePrimaryContainerTakeItsTextInk() throws {
        let sites: [(path: String, snippet: String)] = [
            ("Addresses/AddressManagerView.swift", "Text(L10n.AddressManager.defaultBadge)"),
            (
                "Booking/WhenWhere/AddressPicker/BookingSavedAddressChooser.swift",
                "Text(L10n.AddressManager.defaultBadge)"
            ),
            ("Profile/CustomerDevicesView.swift", "Text(L10n.Devices.thisDevice)"),
            ("Orders/OrderDetailDetailsCards.swift", "Text(String(displayName.prefix(1)).uppercased())")
        ]
        for site in sites {
            let source = try compactSource(site.path)
            let text = try XCTUnwrap(source.range(of: site.snippet), site.path)
            let modifiers = source[text.upperBound...].prefix(120)
            XCTAssertTrue(
                modifiers.contains(".foregroundColor(CleansiaColors.primaryTextOnContainer)"),
                "\(site.path): \(modifiers)"
            )
        }
        let dark = UITraitCollection(userInterfaceStyle: .dark)
        func darkRGB(_ color: Color) -> RGB {
            var red: CGFloat = 0
            var green: CGFloat = 0
            var blue: CGFloat = 0
            var alpha: CGFloat = 0
            UIColor(color).resolvedColor(with: dark).getRed(&red, green: &green, blue: &blue, alpha: &alpha)
            return RGB(Double(red), Double(green), Double(blue))
        }
        let container = darkRGB(CleansiaColors.primaryContainer)
        XCTAssertGreaterThanOrEqual(contrast(darkRGB(CleansiaColors.primaryTextOnContainer), container), 4.5)
        XCTAssertLessThan(contrast(darkRGB(CleansiaColors.primaryText), container), 4.5)
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
