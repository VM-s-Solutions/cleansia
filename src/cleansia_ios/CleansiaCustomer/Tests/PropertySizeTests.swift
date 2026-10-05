import CleansiaCore
import SwiftUI
import XCTest
@testable import CleansiaCustomer

/// Every basket validator refuses a home above `BookingPolicy.MaxRooms` / `MaxBathrooms`
/// (`order.size_exceeds_maximum`), so a stepper that goes one further offers a booking that cannot be
/// made. The caps are read from the policy itself, so the two cannot drift apart unnoticed. Android's
/// `PropertySizeTest` holds the same line.
final class PropertySizeTests: XCTestCase {
    func testTheRoomCapIsTheServers() throws {
        XCTAssertEqual(PropertySize.maxRooms, try Self.policyInt("MaxRooms"))
    }

    func testTheBathroomCapIsTheServers() throws {
        XCTAssertEqual(PropertySize.maxBathrooms, try Self.policyInt("MaxBathrooms"))
    }

    func testBothBookingFlowsCapTheirSteppers() throws {
        for path in [
            "CleansiaCustomer/Sources/Features/Booking/Steps/ServicesStep.swift",
            "CleansiaCustomer/Sources/Features/Recurring/CreateRecurringScreen.swift"
        ] {
            let source = try String(contentsOf: Self.iosRoot().appendingPathComponent(path), encoding: .utf8)
            XCTAssertTrue(source.contains("maximum: PropertySize.maxRooms"), "\(path) lets rooms run past the cap")
            XCTAssertTrue(
                source.contains("maximum: PropertySize.maxBathrooms"),
                "\(path) lets bathrooms run past the cap"
            )
        }
    }

    /// The caps are stated, not only enforced: both flows carry the caption on their size title's row,
    /// above the steppers (owner remark 2026-10-04: at the bottom it took too much room), and the one-off
    /// minus stops at 1 the way the plus stops at the cap.
    func testBothBookingFlowsStateTheCapOnTheTitlesRowAndTheOneOffMinusStopsAtOne() throws {
        for (path, size) in [
            ("CleansiaCustomer/Sources/Features/Booking/Steps/ServicesStep.swift", "struct PropertyRow"),
            ("CleansiaCustomer/Sources/Features/Recurring/CreateRecurringScreen.swift", "struct PropertySizeSection")
        ] {
            let source = try String(contentsOf: Self.iosRoot().appendingPathComponent(path), encoding: .utf8)
            let section = try XCTUnwrap(source.range(of: size), "\(path) has no size section")
            let body = source[section.lowerBound...]
            let title = try XCTUnwrap(body.range(of: "SizeLimitTitleRow {"), "\(path) does not state the cap")
            XCTAssertTrue(body[title.upperBound...].contains("L10n.Booking.yourHome"), "\(path): no home title")
            let steppers = try XCTUnwrap(body.range(of: "maximum: PropertySize.maxRooms"))
            XCTAssertLessThan(title.lowerBound, steppers.lowerBound, "\(path) states the cap under the steppers")
            XCTAssertFalse(source.contains("Text(L10n.Booking.sizeLimitCaption)"), "\(path) states the cap twice")
        }
        let row = try String(
            contentsOf: Self.iosRoot().appendingPathComponent(
                "CleansiaCustomer/Sources/Features/Booking/Steps/ServicesStepComponents.swift"
            ),
            encoding: .utf8
        )
        XCTAssertTrue(row.contains("Text(L10n.Booking.sizeLimitCaption)"))
        let oneOff = try String(
            contentsOf: Self.iosRoot().appendingPathComponent(
                "CleansiaCustomer/Sources/Features/Booking/Steps/ServicesStep.swift"
            ),
            encoding: .utf8
        )
        XCTAssertEqual(oneOff.components(separatedBy: "minimum: 1,").count - 1, 2, "both one-off minuses stop at 1")
    }

    /// The numbers are the format arguments, never words of the translation, in every language.
    func testTheCaptionStatesTheCapsInEveryLanguage() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for language in ["en", "cs", "sk", "uk", "ru"] {
            let path = try XCTUnwrap(
                [Bundle.main, Bundle(for: Self.self)].lazy
                    .compactMap { $0.path(forResource: language, ofType: "lproj") }
                    .first,
                "no \(language).lproj in the built bundle"
            )
            L10n.bundle = try XCTUnwrap(Bundle(path: path))
            let raw = L10n.localized("booking_size_limit_caption")
            XCTAssertNotEqual(raw, "booking_size_limit_caption", "missing in \(language)")
            XCTAssertTrue(raw.contains("%1$lld") && raw.contains("%2$lld"), "\(language) lost a placeholder: \(raw)")
            let residue = raw.replacingOccurrences(of: #"%\d+\$lld"#, with: "", options: .regularExpression)
            XCTAssertNil(residue.rangeOfCharacter(from: .decimalDigits), "\(language) names a number: \(raw)")
            let caption = L10n.Booking.sizeLimitCaption
            XCTAssertTrue(caption.contains("\(PropertySize.maxRooms)"), "\(language): \(caption)")
            XCTAssertTrue(caption.contains("\(PropertySize.maxBathrooms)"), "\(language): \(caption)")
        }
    }

    /// The caption sits on the title's row, trailing, wherever the two fit on one line, and drops to its
    /// own line under the title where they do not — a 320pt phone in Ukrainian — rather than squeezing
    /// either of them.
    @MainActor
    func testTheCaptionSharesTheTitlesRowWhereItFitsAndDropsUnderItWhereItDoesNot() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        // The size card's content width on an iPhone 17 Pro (402pt less the step's 20pt and the card's
        // 12pt padding on each side) and on a 320pt phone.
        let wide: CGFloat = 338
        let narrow: CGFloat = 256
        for (language, width, fits) in [("en", wide, true), ("uk", wide, true), ("uk", narrow, false)] {
            L10n.bundle = try Self.bundle(language)
            let title = Text(L10n.Booking.yourHome).font(CleansiaTypography.labelLarge)
            let titleHeight = Self.height(of: title, width: width)
            let captionHeight = Self.height(
                of: Text(L10n.Booking.sizeLimitCaption).font(CleansiaTypography.labelSmall),
                width: width
            )
            let row = Self.height(of: SizeLimitTitleRow { title }, width: width)
            if fits {
                XCTAssertLessThan(
                    row,
                    titleHeight + captionHeight / 2,
                    "\(language) at \(width)pt: the caption left the title's row though both fit"
                )
            } else {
                XCTAssertGreaterThanOrEqual(
                    row,
                    titleHeight + captionHeight - 1,
                    "\(language) at \(width)pt: the caption stayed on a row too narrow for both"
                )
            }
        }
    }

    @MainActor
    private static func height(of view: some View, width: CGFloat) -> CGFloat {
        UIHostingController(rootView: view)
            .sizeThatFits(in: CGSize(width: width, height: .greatestFiniteMagnitude))
            .height
    }

    private static func bundle(_ language: String) throws -> Bundle {
        let path = try XCTUnwrap(
            [Bundle.main, Bundle(for: PropertySizeTests.self)].lazy
                .compactMap { $0.path(forResource: language, ofType: "lproj") }
                .first,
            "no \(language).lproj in the built bundle"
        )
        return try XCTUnwrap(Bundle(path: path))
    }

    private static func policyInt(_ name: String) throws -> Int {
        let policy = iosRoot()
            .deletingLastPathComponent()
            .appendingPathComponent("Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs")
        let source = try String(contentsOf: policy, encoding: .utf8)
        let pattern = #"public\s+const\s+int\s+"# + name + #"\s*=\s*(\d+)\s*;"#
        let regex = try NSRegularExpression(pattern: pattern)
        let match = try XCTUnwrap(
            regex.firstMatch(in: source, range: NSRange(source.startIndex..., in: source)),
            "BookingPolicy.\(name) not found — the parser needs updating"
        )
        let digits = try XCTUnwrap(Range(match.range(at: 1), in: source))
        return try XCTUnwrap(Int(source[digits]))
    }

    private static func iosRoot() -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
    }
}
